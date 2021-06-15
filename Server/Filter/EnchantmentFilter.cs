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
        public override IEnumerable<object> Options => Enum.GetNames(typeof(Enchantment.EnchantmentType));
        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            return query;
        }
    }

    public class ItemIdFilter : GeneralFilter
    {
        public override FilterType FilterType => FilterType.Equal | FilterType.NUMERICAL;
        public override IEnumerable<object> Options => new object[] { 1, 1000 };

        public override Func<DBItem, bool> IsApplicable => i => false;

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

        public override IQueryable<SaveAuction> AddQuery(IQueryable<SaveAuction> query, FilterArgs args)
        {
            var enchant = Enum.Parse<Enchantment.EnchantmentType>(args.Filters["Enchantment"]);
            var lvl = (short)args.GetAsLong(this);
            var itemid = int.Parse(args.Filters["ItemId"]);
            Console.WriteLine(itemid);
            return query.Where(a => a.Enchantments.Where(e => itemid == e.ItemType && e.Type == enchant && e.Level == lvl).Any());
        }
    }
}
