﻿using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections;
using System.Data.SQLite;
using System.Reflection.Emit;
using System.Linq.Expressions;
using System.Threading;
using MapReduce.NET.Output;
using System.Diagnostics;

namespace MapReduce.NET
{
    public class MapReduceDriver
    {
        public IList<MapReduceTask> Tasks { get; set; }

        public event ProgressDetails Progress;

        private object reducer;
        private object mapper;
        private MethodInfo miMap;
        private MethodInfo miRed;

        private Type MAPKEY;
        private Type MAPVALUE;
        private Type REDUCEKEY;
        private Type REDUCEVALUE;
        private Type REDUCEDNEWVALUE;

        Stopwatch sw;
        long lastStatusUpdate;
        long outPutstartTime;
        private bool completed;

        public MapReduceDriver(string configFileName)
        {
            //Tasks = new List<MapReduceTask>();
            //Tasks.Add(new MapReduceTask { MapName = "TestProj.LogMapper", ReduceName = "TestProj.Reducer1", Input = new IOTask { Location = "input.txt" }, Output = new IOTask { Location = "out.txt" } });
            //string json = JsonConvert.SerializeObject(Tasks);

            if (string.IsNullOrEmpty(Path.GetExtension(configFileName)))
                configFileName += ".json";

            string config = File.ReadAllText(configFileName);

            Tasks = JsonConvert.DeserializeObject<List<MapReduceTask>>(config);
        }

        public void ExecuteStep(MapReduceTask task, IEnumerable input = null)
        {
            object dictMap;
            MethodInfo miExecute = InitTaskExecute(task, out dictMap);

            task.MapThread = new Thread(new ThreadStart(() => miExecute.Invoke(task, new[] { input, mapper, reducer, dictMap, task.ReduceResult, true, false })));
            task.ReduceThread = new Thread(new ThreadStart(() => miExecute.Invoke(task, new[] { input, mapper, reducer, dictMap, task.ReduceResult, false, true })));


            task.MapThread.Start();
            task.ReduceThread.Start();
        }

        private MethodInfo InitTaskExecute(MapReduceTask task, out object dictMap)
        {
            IDictionary tmpdict;

            task.FindTypesMap(out miMap, out dictMap, out mapper);
            task.FindTypesReduce(out miRed, out tmpdict, out reducer);
            task.ReduceResult = tmpdict;

            MethodInfo miExecuteGeneric = task.GetType().GetMethod("ExecuteMapReduce", BindingFlags.NonPublic | BindingFlags.Instance);

            ParameterInfo[] mapParams = mapper.GetType().GetMethod("Map").GetParameters();
            Type[] collectorParams = mapParams[2].ParameterType.GetGenericArguments();

            ParameterInfo[] reduceParams = reducer.GetType().GetMethod("Reduce").GetParameters();


            MAPKEY = mapParams[0].ParameterType;
            MAPVALUE = mapParams[1].ParameterType;
            REDUCEKEY = collectorParams[0];
            REDUCEVALUE = collectorParams[1];
            REDUCEDNEWVALUE = reduceParams[2].ParameterType;

            MethodInfo miExecute = miExecuteGeneric.MakeGenericMethod(new[] { MAPKEY, MAPVALUE, REDUCEKEY, REDUCEVALUE, REDUCEDNEWVALUE });
            return miExecute;
        }

        private MethodInfo InitMergeDictionaries(MapReduceTask task)
        {
            MethodInfo miMergeGeneric = task.GetType().GetMethod("MergeDictionaries", BindingFlags.NonPublic | BindingFlags.Instance);

            MethodInfo miExecute = miMergeGeneric.MakeGenericMethod(new[] { REDUCEKEY, REDUCEVALUE, REDUCEDNEWVALUE });
            return miExecute;
        }

        public void WaitForComplete()
        {
            foreach (var task in Tasks)
            {
                task.WaitForComplete();
            }

            completed = true;
        }

        public IDictionary Start()
        {
            sw = Stopwatch.StartNew();

            completed = false;

            ThreadPool.QueueUserWorkItem(new WaitCallback(WatchDog));

            //execute all tasks
            for (int i = 0; i < Tasks.Count; i++)
            {
                var task = Tasks[i];
                task.SetStopWatch(sw);
                task.StatusUpdate += new StatusDelegate(outp_StatusUpdate);

                // process the result of previous MapReduce tasks
                if (task.Input == null)
                {
                    var inputTask = Tasks[i - 1];

                    for (int j = 0; j < i - 1; j++)
                    {
                        var mergeTask = Tasks[j];
                        MergeDictionaries(inputTask, mergeTask.ReduceResult);
                    }

                    ExecuteStep(task, inputTask.ReduceResult);
                }
                else
                {
                    ExecuteStep(task);
                }

                // if not parallel execution, wait for complete and merge with previous results, then save 
                if (!task.Parallel)
                {
                    task.WaitForComplete();

                    if (task.Output != null && task.Output.PluginType != null)
                    {
                        for (int j = 0; j < i; j++)
                        {
                            var prevTask = Tasks[j];
                            MergeDictionaries(task, prevTask.ReduceResult);
                        }

                        OutputPlugin outp = task.Output.GetPlugin() as OutputPlugin;

                        if (sw != null)
                            outPutstartTime = sw.ElapsedMilliseconds;
                        
                        outp.StatusUpdate += new StatusDelegate(outp_StatusUpdate);

                        outp.Save(task.ReduceResult);

                        task.ReduceResult = null;  // clear memory after saving
                    }
                }
            }

            // wait for all pending tasks
            WaitForComplete();

            var lastTask = Tasks.Last();

            int mergeCount = 0;

            // if there were parallel tasks, merge all of them then save
            for (int j = 0; j < Tasks.Count - 1; j++)
            {
                var prevTask = Tasks[j];

                if (!prevTask.Parallel)
                    continue;

                mergeCount++;

                MergeDictionaries(lastTask, prevTask.ReduceResult);
            }

            if (mergeCount > 0 && lastTask.Output != null)
            {
                OutputPlugin outp = lastTask.Output.GetPlugin() as OutputPlugin;
                outp.Save(lastTask.ReduceResult);
            }

            return lastTask.ReduceResult;
        }

        private void WatchDog(object o)
        {
            while (!completed)
            {
                Thread.Sleep(5000);
                //PartialSave();
#warning Check memory limit and invoke save if running out of memory
            }
        }

        public void PartialSave()
        {
            foreach (var task in Tasks)
            {
                if (task == null || !task.IsRunning || task.Output == null)
                    continue;

                task.PartialSaveInProgress = true;

                OutputPlugin outp = task.Output.GetPlugin() as OutputPlugin;

                if (sw != null)
                    outPutstartTime = sw.ElapsedMilliseconds;

                var status = new StatusDelegate(outp_StatusUpdate);

                outp.StatusUpdate += status;

                outp.Save(task.ReduceResult);

                task.Output.Location = Guid.NewGuid() + ".db3";

                outp.StatusUpdate -= status;

                task.ReduceResult.Clear();  // clear memory after saving

                GC.Collect();

                task.PartialSaveInProgress = false;
            }
        }

        void outp_StatusUpdate(UpdateType type, IUpdateSource source, uint processedItems)
        {
            // if the update is too frequent, slow it down
            if (lastStatusUpdate == 0)
            {
                lastStatusUpdate = sw.ElapsedMilliseconds;
            }
            else if (sw.ElapsedMilliseconds - lastStatusUpdate < 800)
            {
                source.ReportEveryNth *= 2;
            }
            else if (sw.ElapsedMilliseconds - lastStatusUpdate > 1200)
            {
                source.ReportEveryNth = (uint)(source.ReportEveryNth / 1.5f);
            }

            lastStatusUpdate = sw.ElapsedMilliseconds;

            uint itemsPerSec = (uint)(processedItems / (sw.ElapsedMilliseconds / 1000f - outPutstartTime / 1000f));

            string currentAction = "Processing";

            switch (type)
            {
                case UpdateType.Map:
                    break;
                case UpdateType.Reduce:
                    break;
                case UpdateType.Input:
                    return; // currently not interesed in Load events
                    //currentAction = "Loading";
                    //break;
                case UpdateType.Output:
                    currentAction = "Saving";
                    break;
                default:
                    break;
            }


            Console.WriteLine(string.Format("[{1,2}.{2:000}] {4} item #{0:0,0} (avg: {3:0,0}/sec)", 
                processedItems,
                (int)sw.Elapsed.TotalSeconds, 
                sw.Elapsed.Milliseconds, 
                itemsPerSec,
                currentAction
                )
                );

            if (Progress != null)
                Progress(type, processedItems, sw.Elapsed.TotalSeconds, itemsPerSec);
        }

        private void MergeDictionaries(MapReduceTask task, IDictionary from)
        {
            if (task == null || from == null)
                return;

            MethodInfo miMerge = InitMergeDictionaries(task);

            miMerge.Invoke(task, new object[] { reducer, from });
        }
    }
}
