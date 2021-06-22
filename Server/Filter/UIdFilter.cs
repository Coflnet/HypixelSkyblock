using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class UIdFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal | FilterType.RANGE;
        public override IEnumerable<object> Options => new object[] { "000000000000", "ffffffffffff" };

        public override Func<DBItem, bool> IsApplicable => item 
                    => item?.Category.HasFlag(Category.WEAPON) ?? false 
                    || item.Category.HasFlag(Category.ARMOR)
                    || item.Category.HasFlag(Category.CONSUMABLES)
                    || item.Tag.StartsWith("PET_");

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var key = NBT.GetLookupKey("uid");
            var val = NBT.UidToLong(args.Get(this));
            Console.WriteLine("uuid as int " + val);
            return query.Where(a => a.NBTLookup.Where(l=>l.KeyId == key && l.Value == val).Any());
        }
    }
}
