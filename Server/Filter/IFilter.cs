using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public interface IFilter
    {
        string Name { get; }
        /// <summary>
        /// Is this filter available for a given item?
        /// </summary>
        Func<DBItem, bool> IsApplicable { get; }
        FilterType FilterType { get; }
        IEnumerable<object> Options {get; }

        IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args);

        IEnumerable<SaveAuction> Filter(IEnumerable<SaveAuction> items, FilterArgs args);
    }
}
