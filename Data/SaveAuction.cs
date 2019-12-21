using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using fNbt;
using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    [MessagePackObject]
    public class SaveAuction {
        [Key (0)]
        public string Uuid {get;set;}
        [Key (1)]
        public bool Claimed;
        [Key (2)]
        public int Count { get; set;}
        [Key (3)]
        public long StartingBid { get; set;}
        [Key (4)]
        public string Tier { get; set;}
        [Key (5)]
        public string Category;
        // extra is just the ItemName plus the vanillia item type
        //[Key (6)]
        //public string Extra;
       // [Key (7)]
        //public string ItemLore;
        [Key (8)]
        public string ItemName { get; set;}
        [Key (9)]
        public DateTime Start { get; set;}
        [Key (10)]
        public DateTime End { get; set;}
        [Key (11)]
        public string Auctioneer {get;set;}
        [Key (12)]
        public string ProfileId;
        [Key (13)]
        public List<string> Coop;
        [Key (14)]
        public List<object> ClaimedBidders;
        [Key (15)]
        public long HighestBidAmount { get; set;}
        [Key (16)]
        public List<SaveBids> Bids;
        [Key (17)]
        public short AnvilUses;
        [Key(18)]
        public List<Enchantment> Enchantments;

        public SaveAuction (Hypixel.NET.SkyblockApi.Auction auction) {
            ClaimedBidders = auction.ClaimedBidders;
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            Count = NBT.CountFromNBT(auction.ItemBytes);
            AnvilUses = NBT.AnvilUsesFromNBT(auction.ItemBytes);
            Enchantments = NBT.Enchantments(auction.ItemBytes);
            StartingBid = auction.StartingBid;
            Tier = auction.Tier;
            Category = auction.Category;
            //Extra = auction.Extra;
            //ItemLore = auction.ItemLore;
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;
            ProfileId = auction.ProfileId;
            Auctioneer = auction.Auctioneer;
            Uuid = auction.Uuid;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = new List<SaveBids> ();
            foreach (var item in auction.Bids) {
                Bids.Add (new SaveBids (item));
            }
        }

        public SaveAuction () { }

        public override bool Equals(object obj)
        {
            return obj is SaveAuction auction &&
                   Uuid == auction.Uuid;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Uuid);
            
            return hash.ToHashCode();
        }
    }


    class NBT
    {
        public static short CountFromNBT(string data)
        {
            return PropertyFromName(data,"Count");
        }

        public static short AnvilUsesFromNBT(string data)
        {
            return PropertyFromName(data,"anvil_uses",3);
        }


        private static short PropertyFromName(string data, string name, int offset = 0)
        {
            var unziped = Unzip(Convert.FromBase64String(data));
            var index = unziped.IndexOf(name);
            if(index < 0)
            {
                return -1;
            }
            return (short)unziped.Substring(index+name.Length+offset,1)[0];
        }

        public static string SkullUrl(string data)
        {
            var f = new NbtFile();
            var stream = new MemoryStream(Convert.FromBase64String(data));
            f.LoadFromStream(stream,NbtCompression.GZip);
            string base64 = null;
            try{
                base64 = f.RootTag.Get<NbtList>("i")
                    .Get<NbtCompound>(0)
                    .Get<NbtCompound>("tag")
                    .Get<NbtCompound>("SkullOwner")
                    .Get<NbtCompound>("Properties")
                    .Get<NbtList>("textures")
                    .Get<NbtCompound>(0)
                    .Get<NbtString>("Value").StringValue;
            } catch(Exception e)
            {
                Console.WriteLine("Error in parsing "+ f.ToString());
            }
            

            //Console.WriteLine(base64);
            base64 = base64.Replace('-', '+');
            base64 = base64.Replace('_', '/');

            string json = null;
            try{
                json = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));
            } catch (Exception)
            {
                // somethimes the "==" is missing idk why
                json = Encoding.UTF8.GetString(Convert.FromBase64String(base64 + "=="));
                Console.WriteLine(json);
                
                //return null;
            }


            
            dynamic result = JsonConvert.DeserializeObject(json);
            return result.textures.SKIN.url;
        }


        public static List<Enchantment> Enchantments(string data)
        {
            var unziped = Unzip(Convert.FromBase64String(data));
            var start = unziped.IndexOf("enchantments");

            var result = new List<Enchantment>();
            if(start < 0)
            {
                return null;
            }
            

            foreach (var ench in Enum.GetNames(typeof(Enchantment.EnchantmentType)))
            {
                var index = unziped.IndexOf(ench,start);
                if(index > 0)
                {
                    var level = (short)unziped.Substring(index + ench.Length+3,1)[0];
                    Enum.TryParse(ench, out Enchantment.EnchantmentType type);

                    result.Add(new Enchantment(type,level));
                }
            }
            return result;// (int)unziped.Substring(start+5,1)[0];
        }


          public static string Unzip (byte[] bytes) {
            using (var msi = new MemoryStream (bytes))
            using (var mso = new MemoryStream ()) {
                using (var gs = new GZipStream (msi, CompressionMode.Decompress)) {
                    //gs.CopyTo(mso);
                    CopyTo (gs, mso);
                }

                return Encoding.UTF8.GetString (mso.ToArray ());
            }
        }
        public static void CopyTo (Stream src, Stream dest) {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read (bytes, 0, bytes.Length)) != 0) {
                dest.Write (bytes, 0, cnt);
            }
        }
    }
}