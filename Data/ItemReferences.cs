using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class ItemReferences
    {
        private static HashSet<string> reforges = new HashSet<string>(){
            "Demonic",
            "Forceful",
            "Gentle",
            "Godly",
            "Hurtful",
            "Keen",
            "Strong",
            "Superior",
            "Unpleasant",
            "Zealous",
            "Odd",
            "Rich",
            "Epic",
            "Fair",
            "Fast",
            "Heroic",
            "Legendary",
            "Spicy",
            "Deadly",
            "Fine",
            "Grand",
            "Hasty",
            "Neat",
            "Papid",
            "Unreal",
            "Clean",
            "Fierce",
            "Heavy",
            "Light",
            "Mythic",
            "Pure",
            "Smart",
            "Titanic",
            "Wise",
            "Very", 
            "Highly",
            "Bizarre",
            "Itchy",
            "Omnious",
            "Pleasant",
            "Pretty",
            "Shiny",
            "Simple",
            "Strange",
            "Vivid",
            "Ominous"
        };

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
            aote_stone,
            Spiritual,
            Treacherous,
            Renowned,
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



            Unknown = 125,
            Migration = 200
        }


        [Key(0)]
        public string Name;

        [IgnoreMember]
        public ConcurrentBag<string> auctionIds = new ConcurrentBag<string>();

        [Key(2)]
        public ConcurrentBag<AuctionReference> auctions = new ConcurrentBag<AuctionReference>();


        public static string RemoveReforgesAndLevel(string fullItemName)
        {
            if(reforges.Contains(fullItemName.Split(' ')[0]) && fullItemName.Split(' ')[1] != "Dragon" || fullItemName.StartsWith('◆'))
            { 
                int i = fullItemName.IndexOf(" ")+1;
                fullItemName = fullItemName.Substring(i);
            }
            // remove pet level
            return  Regex.Replace(fullItemName,@"\[Lvl \d{1,3}\] ","").Trim();
        }

        /// <summary>
        /// Returns the reforge of an item name
        /// </summary>
        /// <param name="fullItemName"></param>
        /// <returns></returns>
        public static Reforge GetReforges(string fullItemName)
        {
            if(Enum.TryParse(fullItemName.Split(' ')[0],true, out Reforge reforge))
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

            public AuctionReference() {}
        }
    }
}