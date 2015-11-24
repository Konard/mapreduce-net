# Grep MapReduce sample #

One of the simplest tasks for a MapReduce framework to implement the grep functionality, namely to find files based on some content.

The Map in this case consists only of a simple task: try to find the pattern in the given input string. If it finds it, push the result to the output collection, so the reducer can do something with it.

So, to 'visualise' the process:

**InputPlugin**

> file1 - content content patternToFind content...

> file2 - content patternToFind content content...

**Mapper**

> "file1 - … patternToFind " -> Reducer

> "file1 - … patternToFind " -> Reducer

> …

**Reducer**
> List()

> "file1 - … patternToFind "

> "file2 - … patternToFind "
> > -> OutputPlugin

## Mapper ##
If the Mapper finds the content in a line, it passes it on to the reducer. Note the IgnoreCase parameter which is set automatically from the config file.

```
public override void Map(string nullkey, string line, IQueue<string, string> result)
{
    if (
	(IgnoreCase && line.ToLower().IndexOf(Search) > -1) ||
	(!IgnoreCase && line.IndexOf(Search) > -1)
    )
    	result.Push((string)Context.Location, String.Format("{0} - {1}", (int)Context.Position, line));
}
```

## Recuder ##

The Reducer in this case in an empty reducer, just collects the result of the map together into a collection so the output plugin can enumerate on all the results.

```
 public override List<string> Reduce(string key, string value, List<string> result)
{
    if (result == null)
	result = new List<string>();

    result.Add(value);

    return result;
}
```

## Config file ##
```
[
{
        "MapName":"MapReduceSamples.GrepMapper", 
        "ReduceName":"MapReduceSamples.GrepReducer",
        "Input":
                {
                        "Location":"Shakespeare\\*.txt", 
                        "PluginType" : "MapReduce.NET.Input.TextfileLineInput"
                },
        "Output":
                {
                        "PluginType" : "MapReduce.NET.Output.ConsoleOutput"
                },
        "Parameters" : 
                {
                        "search" : "stricken",
                        "ignorecase" : "true"
                }
}
]
```