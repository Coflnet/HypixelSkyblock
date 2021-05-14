using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class EndBeforeFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.LOWER | FilterType.DATE;

        public override IEnumerable<object> Options => new object[]{new DateTime(2019,6,1),DateTime.Now + TimeSpan.FromDays(14)};

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var timestamp = args.GetAsTimeStamp(this);
            return query.Where(a => a.End < timestamp);
        }
    }
}
