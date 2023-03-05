using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Maps properties to items to be able to get their cost
    /// </summary>
    public class PropertyMapper
    {
        private Dictionary<(string, string), (List<string> needed, string previousLevel)> propertyToItem = new()
        {
            { ("upgrade_level", "10"), (new(){"FIFTH_MASTER_STAR"}, "9")},
            { ("upgrade_level", "9"), (new(){"FOURTH_MASTER_STAR"}, "8")},
            { ("upgrade_level", "8"), (new(){"THIRD_MASTER_STAR"}, "7")},
            { ("upgrade_level", "7"), (new(){"SECOND_MASTER_STAR"}, "6")},
            { ("upgrade_level", "6"), (new(){"FIRST_MASTER_STAR"}, string.Empty)},
            { ("rarity_upgrades", "1"), (new(){"RECOMBOBULATOR_3000"}, string.Empty)},
            { ("ethermerge", "1"), (new(){"ETHERWARP_CONDUIT", "ETHERWARP_MERGER"}, string.Empty)},
            { ("artOfPeaceApplied", "1"), (new(){"THE_ART_OF_PEACE"}, string.Empty)},
            { ("art_of_war_count", "1"), (new(){"THE_ART_OF_WAR"}, string.Empty)},
            { ("wood_singularity_count", "1"), (new(){"WOOD_SINGULARITY"}, string.Empty)},
        };

        public bool TryGetIngredients(string property, string value, string baseValue, out List<string> ingredients )
        {
            if(!propertyToItem.TryGetValue((property, value), out var result))
            {
                ingredients = null;
                return false;
            }
            ingredients = new(result.needed);
            if(baseValue != string.Empty && result.previousLevel != baseValue && TryGetIngredients(property, result.previousLevel, baseValue, out var previousLevelIngredients))
                ingredients.AddRange(previousLevelIngredients);
            return true;
        }
    }
}