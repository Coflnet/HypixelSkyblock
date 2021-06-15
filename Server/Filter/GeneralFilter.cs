using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public abstract class GeneralFilter : IFilter
    {
        public string Name => this.GetType().Name.Replace("Filter", "");

        public virtual Func<DBItem, bool> IsApplicable => item => true;

        abstract public FilterType FilterType {get;}

        abstract public IEnumerable<object> Options {get;}

        abstract public IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args);

        public IEnumerable<SaveAuction> Filter(IEnumerable<SaveAuction> items, FilterArgs args)
        {
            return items;
        }
    }
}
