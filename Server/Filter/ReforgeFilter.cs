using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class ReforgeFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal ;
        public override IEnumerable<object> Options => Enum.GetNames(typeof(ItemReferences.Reforge)).Where(e=>e != "Unkown").OrderBy(e=>e);

        public override Func<DBItem, bool> IsApplicable => item 
                => item.Category == Category.ACCESSORIES 
                || item.Category == Category.WEAPON 
                || item.Category == Category.ARMOR;

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var rarity = Enum.Parse<ItemReferences.Reforge>(args.Get(this));
            return query.Where(a => a.Reforge == rarity);
        }
    }
}
