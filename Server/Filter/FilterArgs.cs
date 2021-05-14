using System;
using System.Collections.Generic;

namespace hypixel.Filter
{
    public class FilterArgs
    {
        public Dictionary<string, string> Filters { get; }

        public FilterArgs(Dictionary<string, string> filters)
        {
            Filters = filters;
        }

        public DateTime GetAsTimeStamp(IFilter filter)
        {
            return GetAsLong(filter).ThisIsNowATimeStamp();
        }
        public long GetAsLong(IFilter filter)
        {
            return long.Parse(Get(filter));
        }

        public string Get(IFilter filter)
        {
            return Filters[filter.Name];
        }
    }
}
