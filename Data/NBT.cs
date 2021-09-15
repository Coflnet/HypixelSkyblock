using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using dev;
using fNbt;
using Newtonsoft.Json;

namespace hypixel
{
    public class NBT
    {
        public static string SkullUrl(string data)
        {
            var f = File(Convert.FromBase64String(data));
            return SkullUrl(f);
        }

        private static string SkullUrl(NbtFile file)
        {
            string base64 = null;
            try
            {
                base64 = file.RootTag.Get<NbtList>("i")
                    .Get<NbtCompound>(0)
                    .Get<NbtCompound>("tag")
                    .Get<NbtCompound>("SkullOwner")
                    .Get<NbtCompound>("Properties")
                    .Get<NbtList>("textures")
                    .Get<NbtCompound>(0)
                    .Get<NbtString>("Value").StringValue;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in parsing {file.ToString()} {e.Message}");
            }

            //Console.WriteLine(base64);
            base64 = base64.Replace('-', '+');
            base64 = base64.Replace('_', '/');

            string json = null;
            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));
            }
            catch (Exception)
            {
                // somethimes the "==" is missing idk why
                try
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim() + "="));
                }
                catch (Exception)
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(base64 + "=="));
                }
                Console.WriteLine("Skull url json" + json);

                //return null;
            }

            dynamic result = JsonConvert.DeserializeObject(json);
            return result.textures.SKIN.url;
        }

        public static void FillDetails(SaveAuction auction, string itemBytes)
        {
            var f = File(Convert.FromBase64String(itemBytes));
            var a = f.RootTag.ToString();
            auction.Tag = ItemID(f).Truncate(40);
            auction.Enchantments = Enchantments(f);
            auction.AnvilUses = AnvilUses(f);
            auction.Count = Count(f);
            auction.ItemCreatedAt = GetDateTime(f);
            auction.Reforge = GetReforge(f);
            auction.NbtData = new NbtData(f);
        }

        static readonly ConcurrentBag<string> ValidKeys = new ConcurrentBag<string>()
        {
            "effect",
            "cake_owner",
            "dungeon_skill_req",
            "backpack_color",
            "potion",
            "potion_type",
            "party_hat_color",
            "initiator_player",
            "drill_part_engine",
            "drill_part_fuel_tank",
            "drill_part_upgrade_module",
            "captured_player",
            "potion_name",
            "dungeon_paper_id",
            "leaderboard_player",
            "event",
            "mob_id",
            "entity_required",
            // "last_potion_ingredient",
            "talisman_enrichment",
            "repelling_color",
            "fungi_cutter_mode",
            "builder's_wand_data",
            "jumbo_backpack_data",
            "greater_backpack_data",
            "medium_backpack_data",
            "large_backpack_data",
            "small_backpack_data",
            "spray",
            "new_year_cake_bag_data",

            "ability_scroll",
            "mixins",
            "UNIVERSAL_0",
            "UNIVERSAL_0_gem",
            "JADE_0",
            "TOPAZ_0",
            "AMBER_0",
            "RUBY_0",
            "AMETHYST_0",
            "SAPPHIRE_0"
        };

        static readonly ConcurrentBag<string> KeysWithItem = new ConcurrentBag<string>()
        {
            "heldItem",
            "personal_compact_0",
            "personal_compact_1",
            "personal_compact_2",
            "personal_compact_3",
            "personal_compact_4",
            "personal_compact_5",
            "personal_compact_6",
            "personal_compact_7",
            "personal_compact_8",
            "personal_compact_9",
            "personal_compact_10",
            "personal_compact_11",
            "personal_compactor_0",
            "personal_compactor_1",
            "personal_compactor_2",
            "personal_compactor_3",
            "personal_compactor_4",
            "personal_compactor_5",
            "personal_compactor_6",
            "personal_deletor_0",
            "personal_deletor_1",
            "personal_deletor_2",
            "personal_deletor_3",
            "personal_deletor_4",
            "personal_deletor_5",
            "personal_deletor_6",
            "personal_deletor_7",
            "personal_deletor_8",
            "personal_deletor_9",
            "last_potion_ingredient",
            "power_ability_scroll",
            "skin"
        };

        public static List<NBTLookup> CreateLookup(SaveAuction auction)
        {
            var data = auction.NbtData.Data;
            if (data == null || data.Keys.Count == 0)
                return new List<NBTLookup>();

            List<KeyValuePair<string, object>> flatList = FlattenNbtData(data);

            return flatList.Select(attr =>
            {
                var key = attr.Key;
                if (TryAs<short>(attr, out NBTLookup res))
                    return res;
                if (TryAs<int>(attr, out res))
                    return res;
                if (TryAs<byte>(attr, out res))
                    return res;
                if (TryAs<long>(attr, out res))
                    return res;
                if (TryAs<float>(attr, out res))
                    return res;
                if (TryAs<double>(attr, out res))
                    return res;

                if (key == "uid" || key == "uuid")
                    return new NBTLookup(GetLookupKey(key), UidToLong(attr));
                if (key == "spawnedFor" || key == "bossId")
                    return new NBTLookup(GetLookupKey(key), UidToLong(attr));
                if ((key == "hideInfo" || key == "active") && !((bool)attr.Value))
                    return null; // always false
                if (key == "tier" || key == "type") // both already save on auctions table
                    return null;
                if (key == "skin" && data.ContainsKey("petInfo")) // pet skins are prefixed
                    return new NBTLookup(GetLookupKey(key), ItemDetails.Instance.GetItemIdForName("PET_SKIN_" + (attr.Value as string)));
                if (KeysWithItem.Contains(key))
                    return new NBTLookup(GetLookupKey(key), ItemDetails.Instance.GetItemIdForName(attr.Value as string));
                if (ValidKeys.Contains(key))
                {
                    var keyId = GetLookupKey(key);
                    return new NBTLookup(keyId, GetValueId(keyId, attr.Value as string));
                }
                if (key == "color")
                {
                    ColorFiller.Add(auction.Tag, attr.Value as string);
                    return new NBTLookup(GetLookupKey(key), GetColor(attr));
                }
                Console.WriteLine(JSON.Stringify(attr));
                // just save it as strings

                var lookupKey = GetLookupKey(key);
                return new NBTLookup(lookupKey, GetValueId(lookupKey, JsonConvert.SerializeObject(attr.Value)));
            }).Where(a => a != null).ToList();
        }

        public static List<KeyValuePair<string, object>> FlattenNbtData(Dictionary<string, object> data)
        {
            Func<Dictionary<string, object>, IEnumerable<KeyValuePair<string, object>>> flatten = null;

            flatten = dict => dict.SelectMany(kv =>
                                    kv.Value is Dictionary<string, object>
                                        ? flatten((Dictionary<string, object>)kv.Value)
                                        : new List<KeyValuePair<string, object>>() { kv }
                                   );



            try
            {
                UnwrapList(data, "effects");
                UnwrapList(data, "necromancer_souls");
                UnwarpStringArray(data, "ability_scroll");
                UnwarpStringArray(data, "mixins");
                UnwrapJson(data, "petInfo");
            }
            catch (Exception e)
            {
                Console.WriteLine(JSON.Stringify(data));
                Console.WriteLine(e.StackTrace);
                throw e;
            }

            var flatList = flatten(data).ToList();
            return flatList;
        }

        private static void UnwarpStringArray(Dictionary<string, object> data, string stringArrayKey)
        {
            if (data.TryGetValue(stringArrayKey, out object abilityScroll))
            {
                Console.WriteLine(abilityScroll.GetType());
                var list = (abilityScroll as List<object>)
                    .Select(o => o.ToString())
                    .Select(s => s.Replace("TAG_String:", "")
                    .Replace("\"", ""))
                    .OrderBy(a => a);
                data[stringArrayKey] = String.Join(" ", list);
            }
        }

        private static void UnwrapList(Dictionary<string, object> data, string name)
        {

            if (data.ContainsKey(name))
            {
                foreach (var item in (data[name] as List<object>))
                {
                    var kv = (item as Dictionary<string, object>);
                    foreach (var keys in kv)
                    {
                        data[keys.Key] = keys.Value;
                    }
                }
                data.Remove(name);
            }
        }

        private static void UnwrapJson(Dictionary<string, object> data, string key)
        {
            try
            {
                if (data.ContainsKey(key))
                    data[key] = JsonConvert.DeserializeObject<Dictionary<string, object>>(data[key] as string);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not unwrap {JSON.Stringify(data[key])}");
            }
        }

        private static bool TryAs<T>(KeyValuePair<string, object> attr, out NBTLookup value) where T : IComparable, IConvertible, IFormattable
        {
            value = null;
            if (!(attr.Value is T))
                return false;
            value = new NBTLookup(GetLookupKey(attr.Key), Convert.ToInt64(attr.Value));
            return true;
        }

        private static long GetColor(KeyValuePair<string, object> attr)
        {
            var parts = (attr.Value as string).Split(':');
            int result = 0;
            foreach (var item in parts)
            {
                result += int.Parse(item);
                result = result << 8;
            }
            return result;
        }

        private static long UidToLong(KeyValuePair<string, object> attr)
        {
            var hexNum = attr.Value as string;
            return UidToLong(hexNum);
        }

        public static long UidToLong(string hexNum)
        {
            if (hexNum.Length > 16)
                hexNum = hexNum.Substring(24);
            return Convert.ToInt64(hexNum, 16);
        }

        private static ConcurrentDictionary<string, short> Cache = new ConcurrentDictionary<string, short>();

        public static short GetLookupKey(string name)
        {
            lock (Cache)
            {
                if (Cache.Count == 0)
                    using (var context = new HypixelContext())
                    {
                        foreach (var item in context.NBTKeys)
                        {
                            Cache.TryAdd(item.Slug, item.Id);
                        }
                    }
            }
            if (Cache.TryGetValue(name, out short id))
                return id;

            return Cache.AddOrUpdate(name, k =>
            {
                using var context = new HypixelContext();
                id = context.NBTKeys.Where(k => k.Slug == name).Select(k => k.Id).FirstOrDefault();
                if (id != 0)
                    return id;
                try
                {
                    var key = new NBTKey() { Slug = k };
                    context.NBTKeys.Add(key);
                    context.SaveChanges();
                    return key.Id;
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, $"saving new nbtKey {name}");
                    return -1;
                }
            }, (K, v) => v);
        }
        private static ConcurrentDictionary<(short, string), int> ValueCache = new ConcurrentDictionary<(short, string), int>();

        private static int GetValueId(short key, string value)
        {
            if (ValueCache.TryGetValue((key, value), out int id))
                return id;

            using (var context = new HypixelContext())
            {
                var item = context.NBTValues.Where(v => v.KeyId == key && v.Value == value).FirstOrDefault();
                if (item != null)
                    return item.Id;
            }

            return ValueCache.AddOrUpdate((key, value), k =>
            {
                using var context = new HypixelContext();
                id = context.NBTValues.Where(v => v.KeyId == key && v.Value == value).Select(v => v.Id).FirstOrDefault();
                if (id != 0)
                    return id;
                try
                {
                    var key = new NBTValue() { Value = k.Item2, KeyId = k.Item1 };
                    context.NBTValues.Add(key);
                    context.SaveChanges();
                    return key.Id;
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, $"saving new nbtValue {value} for key {key}");
                    return -1;
                }


            }, (K, v) => v);
        }

        public static ItemReferences.Reforge GetReforge(NbtFile f)
        {
            var extra = GetExtraTag(f);
            if (extra == null || !extra.TryGet<NbtString>("modifier", out NbtString modifier))
            {
                return 0;
            }
            if (Enum.TryParse(modifier.StringValue, true, out ItemReferences.Reforge reforge))
            {
                return reforge;
            }
            Logger.Instance.Error($"Reforge {modifier.StringValue} not found");
            return ItemReferences.Reforge.Unknown;

        }

        public static DateTime GetDateTime(NbtFile file)
        {
            var extra = GetExtraTag(file);
            if (extra == null || !extra.TryGet<NbtString>("timestamp", out NbtString timestamp))
            {
                return new DateTime();
            }

            if (DateTime.TryParseExact(
                    timestamp.StringValue,
                    new String[] { "M/d/yy h:mm tt", "M/d/yy h:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            return new DateTime();
        }

        public static byte AnvilUses(NbtFile data)
        {
            var extra = GetExtraTag(data);
            if (extra == null || !extra.TryGet<NbtInt>("anvil_uses", out NbtInt anvilUses))
            {
                return 0;
            }

            return (byte)(anvilUses.IntValue < 100 ? anvilUses.IntValue : 100);
        }

        public static short HotPotatoCount(NbtFile data)
        {
            var extra = GetExtraTag(data);
            if (!extra.TryGet<NbtCompound>("hot_potato_count", out NbtCompound hotPotatoCount))
            {
                return 0;
            }

            return hotPotatoCount.ShortValue;
        }

        public static byte Count(NbtFile file)
        {
            return file.RootTag?.Get<NbtList>("i")
                ?.Get<NbtCompound>(0)
                ?.Get<NbtByte>("Count")?.ByteValue ?? 0;
        }

        private static NbtCompound GetExtraTag(NbtFile file)
        {
            return file?.RootTag?.Get<NbtList>("i")
                ?.Get<NbtCompound>(0)
                ?.Get<NbtCompound>("tag")
                ?.Get<NbtCompound>("ExtraAttributes");
        }

        public static List<Enchantment> Enchantments(NbtFile data)
        {
            var extra = GetExtraTag(data);
            if (extra == null || !extra.TryGet<NbtCompound>("enchantments", out NbtCompound elements))
            {
                return new List<Enchantment>();
            }

            var result = new List<Enchantment>();

            foreach (var item in elements.Names)
            {
                if (!Enum.TryParse(item, true, out Enchantment.EnchantmentType type))
                {
                    Logger.Instance.Error("Did not find Enchantment " + item);
                }
                var level = elements.Get<NbtInt>(item).IntValue;
                result.Add(new Enchantment(type, (byte)level));
            }

            return result;
        }

        public static string ItemID(string input)
        {
            var f = File(Convert.FromBase64String(input));

            return ItemID(f);
        }

        private static string ItemID(NbtFile file)
        {
            var nbt = GetExtraTag(file);

            var id = nbt?.Get<NbtString>("id")?.StringValue;

            if (id == "PET")
            {
                id = GetPetId(nbt, id);
            }
            else if (id == "POTION")
            {
                if (nbt.TryGet<NbtString>("potion", out NbtString potionTag))
                    id += $"_{potionTag.StringValue}";

            }
            else if (id == "RUNE")
            {
                var tag = nbt.Get<NbtCompound>("runes");
                var runeType = tag.Tags.First().Name;
                id += $"_{runeType}";
            }

            return id;

        }

        private static string GetPetId(NbtCompound nbt, string id)
        {
            try
            {
                var tag = nbt.Get<NbtString>("petInfo");
                PetInfo info = JsonConvert.DeserializeObject<PetInfo>(tag.StringValue);
                var petType = info.Type;
                id += $"_{petType}";
                return id;
            }
            catch (Exception)
            {
                Logger.Instance.Error($"Could not get itemId from nbt {nbt?.ToString()}");
                return "PET_unkown";
            }
        }

        public static byte[] Extra(string input)
        {
            var f = File(Convert.FromBase64String(input));

            var a = f.ToString();
            return Extra(f);
        }

        public static byte[] Extra(NbtFile file)
        {
            var tag = GetExtraTag(file);
            if (tag == null)
                return null;



            tag.Remove("enchantments");
            tag.Remove("modifier");
            tag.Remove("hotPotatoBonus");
            tag.Remove("originTag");
            tag.Remove("timestamp");
            tag.Remove("anvil_uses");
            tag.Remove("id");

            if (tag.Contains("uuid"))
            {
                var uuid = tag.Get<NbtString>("uuid");
                tag.Remove("uuid");
                tag.Add(new NbtString("uid", uuid.StringValue.Substring(24)));
            }
            if (tag.Contains("hot_potato_count"))
            {
                // rename to be smaler
                var count = tag.Get<NbtInt>("hot_potato_count");
                count.Name = "hpc";
            }

            tag.Name = "";

            var shortenedFile = new NbtFile(tag);
            return Bytes(shortenedFile);
        }

        public static string Pretty(string input)
        {
            return File(Convert.FromBase64String(input)).ToString();
        }

        public static NbtFile File(byte[] input, NbtCompression compression = NbtCompression.GZip)
        {
            var f = new NbtFile();
            if (input != null)
            {
                var stream = new MemoryStream(input);
                f.LoadFromStream(stream, compression);
            }

            return f;
        }

        public static byte[] Bytes(NbtFile file)
        {
            var outStream = new MemoryStream();
            file.SaveToStream(outStream, NbtCompression.None);
            return outStream.ToArray();
        }
    }
}