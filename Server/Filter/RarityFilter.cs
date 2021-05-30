using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class RarityFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal ;
        public override IEnumerable<object> Options => Enum.GetNames(typeof(Tier));

        public override Func<DBItem, bool> IsApplicable => item => item?.Tag?.StartsWith("PET_") ?? false;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var rarity = Enum.Parse<Tier>(args.Get(this));
            return query.Where(a => a.Tier == rarity);
        }
    }
}
