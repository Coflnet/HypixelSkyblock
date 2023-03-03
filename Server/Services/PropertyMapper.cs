using System.Collections.Generic;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Maps properties to items to be able to get their cost
    /// </summary>
    public class PropertyMapper
    {
        private Dictionary<(string, string), (List<string> needed, string previousLevel)> propertyToItem = new()
        {
            { ("upgrade", "10"), (new(){"FIFTH_MASTER_STAR"}, "9")},
            { ("upgrade", "9"), (new(){"FOURTH_MASTER_STAR"}, "8")},
            { ("upgrade", "8"), (new(){"THIRD_MASTER_STAR"}, "7")},
            { ("upgrade", "7"), (new(){"SECOND_MASTER_STAR"}, "6")},
            { ("upgrade", "6"), (new(){"FIRST_MASTER_STAR"}, "5")}
        };
    }
}