using System;

namespace hypixel.Filter
{
    [Flags]
    public enum FilterType
    {
        Equal = 1,
        HIGHER = 2,
        LOWER = 4,
        DATE = 8,
        NUMERICAL = 16
    }
}
