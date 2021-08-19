using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace hypixel.Filter
{
    public class PetSkinFilter : PetFilter
    {
        public override FilterType FilterType => FilterType.Equal;

        public override IEnumerable<object> Options => ItemDetails.Instance.TagLookup.Keys.Where(k=>k.StartsWith("PET_SKIN"));

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var item = ItemDetails.Instance.GetItemIdForName(args.Get(this));
            var key = NBT.GetLookupKey("skin");
            return query.Include(a=>a.NBTLookup).Where(a => a.NBTLookup.Where(l => l.KeyId == key && l.Value == item).Any());
        }
    }
}
