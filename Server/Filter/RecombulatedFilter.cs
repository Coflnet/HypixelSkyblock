using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class RecombulatedFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal;

        public override IEnumerable<object> Options => new object[] { "yes", "no" };

        public override Func<DBItem, bool> IsApplicable => item
            => (item?.Category == Category.WEAPON)
            || item.Category == Category.ARMOR;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var key = NBT.GetLookupKey("rarity_upgrades");
            var stringVal = args.Get(this);
            if (args.Get(this) == "yes")
                return query.Where(a => a.NBTLookup.Where(l => l.KeyId == key).Any());
            return query.Where(a => !a.NBTLookup.Where(l => l.KeyId == key).Any());
        }
    }
}

