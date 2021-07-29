using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class ItemIdFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal | FilterType.NUMERICAL;
        public override IEnumerable<object> Options => new object[] { 1, 1000 };

        public override Func<DBItem, bool> IsApplicable => i => false;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            return query;
        }

    }
}
