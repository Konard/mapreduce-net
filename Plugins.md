# Plugins #

The Mapreduce.NET framework uses a plugin architecture to read data from any source and write the output to any destination format. Every MapReduce configuration must contain an input plugin and an output plugin to tell the framework how to pass the data to the Mapper and how to save the result from the Reducer.

## Built-in input plugins ##

> InputPlugin`<T>` - the abstract parent for implementing any kind of input plugins

> TextfileLineInput - a simple text file reader, which passes the file line by line to the mapper in the **Value parameter**. The **Context.Position** will be an integer pointing to the beginning of the line in the file.

## Built-in output plugins ##

> OutputPlugin - the abstract parent for implementing any kind of output plugins

> CSVJSONOutput - a simple text file writer that will serialise the Reduced output dictionary into standard JSON format

> ConsoleOutput - will dump the the result to the console. Good for debugging or small data outputs

> SQLiteOutput - will save the Reduce output (key,value) into a SQLite3 db file in JSON format. Will create the file if it does not exist

> SQLiteProtoBufOutput - will save the Reduce output (key,value) into a SQLite3 db file in ProtoBuf format. Will create the file if it does not exist


## Configuration ##

For every input or output plugin, you need to specify a **Location** which will be interpreted by the plugin itself. It can be a directory location or an SQL connection string, it will be passed to the plugin to do something with it. Some plugins (like the ConsoleOutput) will ignore this property so no need to define it in the configuration file.


```
[
{
        "MapName": ..., 
        "ReduceName":...,
        "Input":
                {
                        "Location":"big.001",
                        "PluginType" : "MapReduce.NET.Input.TextfileLineInput"
                },
        "Output":
                {
                        "Location":"buffer1.db3", 
                        "PluginType" : "MapReduce.NET.Output.SQLiteOutput"
                }
}
]
```