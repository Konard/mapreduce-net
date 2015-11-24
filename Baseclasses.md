If you want to build a MapReduce process, you need to define a mapper and a reducer step.
Both definitions need to derive from an abstract parent and needs overriden one-one functions.

### The Mapper ###

```
MapReduce.NET.Mapper<KEY, VALUE, REDUCEKEY, REDUCEVALUE>
        public abstract void Map(KEY key, VALUE value, IQueue<REDUCEKEY,REDUCEVALUE> result);
```

When creating a Map step, one need to define what key/value types are coming in (_KEY_,_VALUE_) and what types are leaving the function (_REDUCEKEY_,_REDUCEVALUE_).
The reducer will receive the map's output so it's essential to reuse the Map's output types as the Reduce input types.
Depending wich input plugin one might use, the KEY and VALUE can have different types. With the simple text file line reader, the KEY stays empty while the VALUE will hold the actual line (String).

### The Reducer ###

```
MapReduce.NET.Reducer<REDUCEKEY, REDUCEVALUE, REDUCEDNEWVALUE> : MapReduceBase
        public virtual void Merge(REDUCEDNEWVALUE to, REDUCEDNEWVALUE from)
        public abstract REDUCEDNEWVALUE Reduce(REDUCEKEY key, REDUCEVALUE value, REDUCEDNEWVALUE result);
```

The Reducer needs at least the Reduce function defined while one can define the Merge method if want to merge together different MapReduce processes' outputs. Although it's worth mentioning that the output of a MapReduce process usually is used by itself and not merged together with other results (simply because different machines will need a sharded data to serve the incoming requests and the chunks created by MapReduce tasks serve as an excellent shard).
The _REDUCEKEY_ and _REDUCEVALUE_,as mentioned before, are the same type as the map's output. It is essential to understand that the Reduce step cannot change the KEY of the output but can modify the outputted value (either change the value like adding 1 to it or completely changing the type too). The Reduce step can be thought of as a GROUP-BY in sql: you can do AVG, MAX, MIN on values but the key of the GROUP-BY will not change.


### Paremeters ###

Every parameter that is configured either in the config file or programatically will be automatically mapped to the typed properties in both Map and Reduce classes. This is the easiest method to pass in extra parameters to either of the steps, like defining what text to look for in a distributed Grep or which words to ignore when building a inverted index (you probably don't want to store the 'the', 'a' and other too frequent words in your database for searching).

```
"Parameters" : 
{
	"search" : "stricken",
	"ignorecase" : "true"
}
```

will be mapped to the Mapper:

```
public class GrepMapper : Mapper<string, string, string, string>
{
        public bool IgnoreCase { get; set; }
        public string Search { get; set; }
...
```