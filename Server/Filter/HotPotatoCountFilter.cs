using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class HotPotatoCountFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal;

        public override IEnumerable<object> Options => new object[] { "1", "15" };

        public override Func<DBItem, bool> IsApplicable => item
            => (item?.Category == Category.WEAPON)
            || item.Category == Category.ARMOR;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var key = NBT.GetLookupKey("hpc");
            var val = args.GetAsLong(this);
            return query.Where(a => a.NBTLookup.Where(l => l.KeyId == key && l.Value == val).Any());
        }
    }
}

