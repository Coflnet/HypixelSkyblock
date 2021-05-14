using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel.Filter
{
    [MessagePackObject]
    public class FilterOptions
    {
        [Key("name")]
        public string Name;
        [Key("options")]
        public IEnumerable<string> Options;
        [Key("type")]
        public FilterType Type;
        [Key("longType")]
        public string LongType;

        public FilterOptions()
        {
        }

        public FilterOptions(IFilter filter)
        {
            Name = filter.Name;
            Options = filter.Options.Select(o=>o.ToString());
            Type = filter.FilterType;
            LongType = Type.ToString();
        }
    }
}
