using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class ItemReferences
    {
        /// <summary>
        /// Reforges as strings to compare name against.
        /// Loaded in the static constructor
        /// </summary>
        public static readonly HashSet<string> reforges;

        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public enum Reforge
        {
            None,
            Demonic,
            Forceful,
            Gentle,
            Godly,
            Hurtful,
            Keen,
            Strong,
            Superior,
            Unpleasant,
            Zealous,
            Odd,
            Rich,
            Epic,
            Fair,
            Fast,
            Heroic,
            Legendary,
            Spicy,
            Deadly,
            Fine,
            Grand,
            Hasty,
            Neat,
            Papid,
            Unreal,
            Clean,
            Fierce,
            Heavy,
            Light,
            Mythic,
            Pure,
            Smart,
            Titanic,
            Wise,
            Very,
            Highly,
            Bizarre,
            Itchy,
            Omnious,
            Pleasant,
            Pretty,
            Shiny,
            Simple,
            Strange,
            Vivid,
            Ominous,
            Sharp,
            Rapid,
            Necrotic,
            Fabled,
            Precise,
            Giant,
            warped_on_aote = 53,
            aoteStone = 53,
            aote_stone = 53,
            Spiritual,
            Treacherous,
            RENOWED,
            Renowned = RENOWED,
            Reinforced,
            rich_bow,
            Spiked,
            Perfect,
            Magnetic,
            Loving,
            Gilded,
            odd_sword,
            Salty,
            Silky,
            Refined,
            suspicious,
            toil,
            empowered,
            fruitful,
            blessed,
            shaded,
            awkward,
            dirty,
            undead,
            cubic,
            bloody,
            moil,
            ridiculous,
            rich_sword,
            warped,
            odd_bow,
            candied,
            submerged,
            ancient,
            withered,
            sweet,
            fleet,
            stellar,
            mithraic,
            Auspicious,
            headstrong,
            stiff,
            bulky,
            lucky,
            bountiful,
            heated,
            jaded,
            ambered,
            double_bit,
            excellent,
            fortunate,
            prospector,
            lush,
            sturdy,
            lumberjack,
            unyielding,
            great,
            robust,
            rugged,
            zooming,
            peasant,
            strengthened,
            glistening,
            waxed,
            fortified,
            green_thumb,
            pitchin,
            coldfusion,
            bustling,
            earthy,
            mossy,
            blooming,
            rooted,
            festive,
            snowy,

            Unknown = -125,
            Any,

            chomp = -100,
            full_jaw_fanging_kit,
            presumed_gallon_of_red_paint,
            displaced_leech,
            Fang_tastic_chocolate_chip,
            bubba_blister,
            fanged,
            blood_soaked,
            stained,
            menacing,
            hefty,
            soft,
            honored,
            blended,
            astute,
            colossal,
            brilliant,
            greater_spook,
            beady,
            buzzing,
            jerry_stone,
            hyper,
            coldfused,
            Earthly,
            glacial,
            lustrous,
        }


        [Key(0)]
        public string Name;

        [IgnoreMember]
        public ConcurrentBag<string> auctionIds = new ConcurrentBag<string>();

        [Key(2)]
        public ConcurrentBag<AuctionReference> auctions = new ConcurrentBag<AuctionReference>();

        static ItemReferences()
        {
            reforges = new HashSet<string>(Enum.GetNames(typeof(Reforge)).Select(name => name.ToLower()));
        }

        public static string RemoveReforgesAndLevel(string fullItemName)
        {
            if (fullItemName == null)
                return fullItemName;
            fullItemName = fullItemName.Trim('✪').Replace("⚚", "").Replace("✦","");
            fullItemName = RemoveReforge(fullItemName);
            // remove pet level
            return Regex.Replace(fullItemName, @"\[Lvl \d{1,3}\] ", "").Trim();
        }

        public static string RemoveReforge(string fullItemName)
        {
            if (fullItemName == null)
                return fullItemName;
            var splitName = fullItemName.Split(' ');
            if (reforges.Contains(splitName[0].ToLower()) && (splitName.Count() == 1 || splitName[1] != "Dragon") || fullItemName.StartsWith('◆'))
            {
                int i = fullItemName.IndexOf(" ") + 1;
                fullItemName = fullItemName.Substring(i);
            }

            return fullItemName;
        }



        /// <summary>
        /// Returns the reforge of an item name
        /// </summary>
        /// <param name="fullItemName"></param>
        /// <returns></returns>
        public static Reforge GetReforges(string fullItemName)
        {
            if (Enum.TryParse(fullItemName.Split(' ')[0], true, out Reforge reforge))
            {
                return reforge;
            }
            return Reforge.None;
        }


        [MessagePackObject]
        public class AuctionReference
        {
            [Key(0)]
            public string uuId;
            [Key(1)]
            public DateTime End;

            public AuctionReference(string uuId, DateTime end)
            {
                this.uuId = uuId;
                End = end;
            }

            public AuctionReference() { }
        }
    }
}