using System;

namespace hypixel.Filter
{
    public abstract class PetFilter : GeneralFilter
    {
        public override Func<DBItem, bool> IsApplicable => item => (item?.Tag?.StartsWith("PET_")  ?? false) 
        && !(item?.Tag?.StartsWith("PET_ITEM") ?? true)
        && !(item?.Tag?.StartsWith("PET_SKIN") ?? true);
    }
}
