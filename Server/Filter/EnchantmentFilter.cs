using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace hypixel.Filter
{
    public class EnchantmentFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal;
        public override Func<DBItem, bool> IsApplicable =>
                EnchantLvlFilter.IsEnchantable();
        public override IEnumerable<object> Options => Enum.GetNames(typeof(Enchantment.EnchantmentType)).OrderBy(e => e);
        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            return query;
        }
    }

    

    public class EnchantLvlFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal;
        public override IEnumerable<object> Options => new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        public override Func<DBItem, bool> IsApplicable =>
                IsEnchantable();

        public static Func<DBItem, bool> IsEnchantable()
        {
            return item => item.Category == Category.WEAPON
                            || item.Category == Category.ARMOR
                            || item.Tag == "ENCHANTED_BOOK";
        }

        public virtual string EnchantmentKey { get; set; } = "Enchantment";

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            if (!args.Filters.ContainsKey(EnchantmentKey))
                throw new CoflnetException("invalid_filter", "You need to select an enchantment and a lvl to filter for");
            var enchant = Enum.Parse<Enchantment.EnchantmentType>(args.Filters[EnchantmentKey]);
            var lvl = (short)args.GetAsLong(this);
            var itemid = int.Parse(args.Filters["ItemId"]);
            return query.Where(a => a.Enchantments.Where(e => itemid == e.ItemType && e.Type == enchant && e.Level == lvl).Any());
        }
    }

    public class SecondEnchantmentFilter : EnchantmentFilter
    {
        
    }

    public class SecondEnchantLvlFilter : EnchantLvlFilter
    {
        public override string EnchantmentKey { get; set; } = "SecondEnchantment";
    }
}
