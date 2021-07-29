using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class StarsFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal;

        public override IEnumerable<object> Options => new object[] { "1", "2", "3", "4", "5", "none" };

        public override Func<DBItem, bool> IsApplicable => item
            => (item?.Category == Category.WEAPON)
            || item.Category == Category.ARMOR;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var key = NBT.GetLookupKey("dungeon_item_level");
            var stringVal = args.Get(this);
            if (int.TryParse(stringVal, out int val))
                return query.Where(a => a.NBTLookup.Where(l => l.KeyId == key && l.Value == val).Any());
            return query.Where(a => !a.NBTLookup.Where(l => l.KeyId == key).Any());
        }
    }
}

