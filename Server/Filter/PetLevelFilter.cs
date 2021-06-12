using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace hypixel.Filter
{
    public class PetLevelFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal | FilterType.NUMERICAL | FilterType.RANGE;
        public override IEnumerable<object> Options => new object[] { 1, 100 };

        public override Func<DBItem, bool> IsApplicable => item => item?.Tag?.StartsWith("PET_") ?? false;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var level = args.GetAsLong(this);
            return query.Where(a => EF.Functions.Like(a.ItemName, $"[Lvl {level}]%"));
        }
    }
}
