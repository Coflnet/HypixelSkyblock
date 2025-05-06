using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Coflnet.Sky.Commands.MC;
using dev;
using fNbt;
using fNbt.Tags;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Coflnet.Sky.Core
{
    public interface INBT
    {
        short GetKeyId(string name);
        int GetValueId(short key, string value);
    }

    public class NBT : INBT
    {
        public static INBT Instance = new NBT();
        public bool CanWriteToDb { get; set; } = false;

        public static string SkullUrl(string data)
        {
            var f = File(Convert.FromBase64String(data));
            return SkullUrl(f);
        }



        private static string SkullUrl(NbtFile file)
        {
            return SkullUrl(file.RootTag.Get<NbtList>("i")
                    .Get<NbtCompound>(0));
        }

        public static string SkullUrl(NbtCompound root)
        {
            string base64 = null;
            try
            {
                base64 = root
                    .Get<NbtCompound>("tag")
                    .Get<NbtCompound>("SkullOwner")?
                    .Get<NbtCompound>("Properties")?
                    .Get<NbtList>("textures")
                    .Get<NbtCompound>(0)
                    .Get<NbtString>("Value").StringValue;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in parsing {root.ToString()} {e.Message}");
            }
            if (string.IsNullOrEmpty(base64))
                return null;

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
                    Console.WriteLine("Invalid base64 " + base64);

                    json = Encoding.UTF8.GetString(Convert.FromBase64String(base64.TrimEnd('=') + "=="));
                }
                Console.WriteLine("Skull url json" + json);

                //return null;
            }

            dynamic result = JsonConvert.DeserializeObject(json);
            return result.textures.SKIN.url;
        }

        public static NbtCompound FillDetails(SaveAuction auction, string itemBytes, bool includeTier = false)
        {
            var f = File(Convert.FromBase64String(itemBytes)).RootTag?.Get<NbtList>("i")
                ?.Get<NbtCompound>(0);
            if (f == null)
            {
                f = File(Convert.FromBase64String(itemBytes)).RootTag;
            }
            FillFromTag(auction, f, includeTier);
            return f;
        }

        public static void FillFromTag(SaveAuction auction, NbtCompound f, bool includeTier)
        {
            auction.Tag = ItemID(f).Truncate(40);

            auction.Enchantments = Enchantments(f);
            if (string.IsNullOrEmpty(auction.Tag))
                TryAssignTagForBazaarBooks(auction, f);
            auction.AnvilUses = AnvilUses(f);
            auction.Count = Count(f);
            auction.ItemCreatedAt = GetDateTime(f);
            auction.Reforge = GetReforge(f);
            if (auction.Context == null)
                auction.Context = new();
            auction.Context["itemUuid"] = Uuid(f);
            if (includeTier)
            {
                foreach (var line in GetLore(f).Reverse())
                {
                    if (GetAndAssignTier(auction, line))
                        break;
                }
            }
            var name = GetName(f);
            if (auction.Tier == Tier.UNKNOWN)
                auction.Tier = name.Replace("§f", "").Substring(0, 2) switch
                {
                    "§c" => Tier.SPECIAL, // god potions don't have it in lore, also they start with two §f
                    "§4" => Tier.ULTIMATE, // skins are sometimes missing the tag
                    _ => Tier.UNKNOWN
                };
            if (auction.Context != null)
                auction.Context["cname"] = name;
            if (auction.ItemName == null)
                auction.ItemName = name;
            if ((f?.TryGet<NbtTag>("id", out var result) ?? false) && (
                result.TagType == NbtTagType.Short && result.ShortValue >= 298 && result.ShortValue <= 301
                || result.TagType == NbtTagType.String && result.StringValue.StartsWith("minecraft:leather_")))
            {
                var intColor = GetColor(f);
                var extra = GetExtraTag(f);
                // to rrr:ggg:bbb
                if (!extra.Contains("color"))
                {
                    var converted = $"{intColor >> 16 & 0xFF}:{intColor >> 8 & 0xFF}:{intColor & 0xFF}";
                    extra.Add(new NbtString("color", converted));
                    extra.Add(new NbtByte("cc", 1)); // "copied color"
                }
            }
            auction.NbtData = new NbtData(f);
        }

        private static string Uuid(NbtCompound f)
        {
            var tag = GetExtraTag(f);
            if (tag == null)
                return null;
            if (!tag.Contains("uuid"))
                return null;
            return tag.Get<NbtString>("uuid").StringValue;
        }

        private static void TryAssignTagForBazaarBooks(SaveAuction auction, NbtCompound f)
        {
            var mcId = f?.Get<NbtString>("id").StringValue;
            if (mcId != "minecraft:enchanted_book")
            {
                return;
            }
            var name = f?.Get<NbtCompound>("tag")?.Get<NbtCompound>("display")?.Get<NbtString>("Name")?.StringValue;
            if (name == "§9Stacking Enchants")
                return; // does not have a real enchantment
            if (name.StartsWith("§a§aBuy §e") || name.StartsWith("BUY_") || name.StartsWith("SELL_"))
                return;
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    // try to get enchantment for bazaar
                    var lastSpace = name.LastIndexOf(' ');
                    var levelString = name.Substring(lastSpace + 1).Split('-').First();
                    if (!int.TryParse(levelString, out int level))
                        if (lastSpace < 0)
                        {
                            level = 1;
                            lastSpace = name.Length + 2;
                        }
                        else
                            level = Roman.From(levelString);
                    var enchantName = name.Substring(2, lastSpace - 2).Replace(' ', '_').Replace('-', '_');
                    while (enchantName.StartsWith("§"))
                        enchantName = enchantName.Substring(2);
                    if (!Enum.TryParse<Enchantment.EnchantmentType>(enchantName, true, out Enchantment.EnchantmentType enchant))
                        if (!Enum.TryParse<Enchantment.EnchantmentType>("ultimate_" + enchantName, true, out enchant))
                            Console.WriteLine("unkown enchant " + enchantName);
                    auction.Tag = "ENCHANTMENT_" + enchant.ToString().ToUpper() + '_' + level;
                }
                catch (Exception e)
                {
                    Logger.Instance.Error(e, "Parsing book name " + name);
                }
            }
        }

        public static bool GetAndAssignTier(SaveAuction auction, string lastLine)
        {
            if (TryFindTierInString(lastLine, out Tier tier))
            {
                auction.Tier = tier;
                return true;
            }
            return false;
        }

        public static bool TryFindTierInString(string lastLine, out Tier tier)
        {
            tier = Tier.UNKNOWN;
            if (lastLine == null)
                return false;
            foreach (var item in TierNames)
            {
                if (lastLine.Contains(item.Key))
                {
                    tier = item.Value;
                    return true;
                }
            }
            return false;
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
            "JADE_1",
            "JADE_2",
            "TOPAZ_0",
            "AMBER_0",
            "AMBER_1",
            "AMBER_2",
            "RUBY_0",
            "AMETHYST_0",
            "AMETHYST_1",
            "AMETHYST_2",
            "SAPPHIRE_0",
            "JASPER_0",
            "RUBY_1",
            "RUBY_2",
            "RUBY_3",
            "RUBY_4",

            "COMBAT_0", // rarity of gem
            "COMBAT_0_gem", // type of gem
            "COMBAT_1", // rarity of gem
            "COMBAT_1_gem", // type of gem
            "DEFENSIVE_0", // rarity of gem
            "DEFENSIVE_0_gem", // type of gem
            "UNIVERSAL_0", // rarity of gem
            "UNIVERSAL_0_gem", // type of gem
            "MINING_0",
            "MINING_0_gem",
            "unlocked_slots",

            "hideRightClick" // looks like it is always false
        };

        static readonly HashSet<string> IgnoreIndexing = new()
        {
            "uniqueId", // unimportant id
            "noMove"
        };

        internal static readonly ConcurrentBag<string> KeysWithItem = new ConcurrentBag<string>()
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
            "skin",
            "dye_item",
            "drill_part_engine",
            "drill_part_fuel_tank",
            "drill_part_upgrade_module",
            "hook.part",
            "line.part",
            "sinker.part",
        };

        public static NBTLookup[] CreateLookup(SaveAuction auction)
        {
            if (auction.NbtData == null)
                return Array.Empty<NBTLookup>();
            var data = auction.NbtData.Data;
            if (data == null || data.Keys.Count == 0)
                return Array.Empty<NBTLookup>();

            List<KeyValuePair<string, object>> flatList = FlattenNbtData(data);
            return CreateLookup(auction.Tag, data, flatList);
        }

        public static NBTLookup[] CreateLookup(string auctionTag, Dictionary<string, object> data, List<KeyValuePair<string, object>> flatList = null)
        {
            flatList ??= FlattenNbtData(data);
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

                if (key == "uid" || key == "uuid" || key.EndsWith(".uuid"))
                    return new NBTLookup(Instance.GetKeyId(key), UidToLong(attr));
                if (key == "spawnedFor" || key == "bossId")
                    return new NBTLookup(Instance.GetKeyId(key), UidToLong(attr));
                if ((key == "hideInfo" || key == "active") && !((bool)attr.Value))
                    return null; // always false
                if (key == "tier" || key == "type") // both already save on auctions table
                    return null;
                if (key == "skin" && data.ContainsKey("petInfo")) // pet skins are prefixed
                    return new NBTLookup(Instance.GetKeyId(key), GetItemIdForSkin(attr.Value as string));
                if (KeysWithItem.Contains(key))
                    return new NBTLookup(Instance.GetKeyId(key), ItemDetails.Instance.GetItemIdForTag(attr.Value as string));
                if (ValidKeys.Contains(key))
                {
                    var keyId = Instance.GetKeyId(key);
                    if (!(attr.Value is string value))
                        value = JsonConvert.SerializeObject(attr.Value);
                    return new NBTLookup(keyId, Instance.GetValueId(keyId, value));
                }
                if (key == "color")
                {
                    ColorFiller.Add(auctionTag, attr.Value as string);
                    return new NBTLookup(Instance.GetKeyId(key), GetColor(attr));
                }
                if (IgnoreIndexing.Contains(key))
                    return null;
                Console.WriteLine("unknown id " + JSON.Stringify(attr));
                // just save it as strings

                var lookupKey = Instance.GetKeyId(key);
                return new NBTLookup(lookupKey, Instance.GetValueId(lookupKey, JsonConvert.SerializeObject(attr.Value)));
            }).Where(a => a != null).ToArray();
        }

        public static long GetItemIdForSkin(string name)
        {
            var id = ItemDetails.Instance.GetItemIdForTag("PET_SKIN_" + name, false);
            if (id == 0)
                id = ItemDetails.Instance.GetItemIdForTag(name);
            return id;
        }

        public static bool IsPet(string tag)
        {
            return tag != null && tag.StartsWith("PET_") && !tag.StartsWith("PET_SKIN") && !tag.StartsWith("PET_ITEM") && tag != "PET_CAKE";
        }

        private static object FromJson(object requestObject)
        {
            switch (requestObject)
            {
                case JObject jObject: // objects become Dictionary<string,object>
                    return ((IEnumerable<KeyValuePair<string, JToken>>)jObject).ToDictionary(j => j.Key, j => FromJson(j.Value));
                case JArray jArray: // arrays become List<object>
                    return jArray.Select(FromJson).ToList();
                case JValue jValue: // values just become the value
                    return jValue.Value;
                default: // no special handling
                    return requestObject;
            }
        }


        public static Dictionary<string, object> FromDeserializedJson(Dictionary<string, object> requestObject)
        {
            return requestObject.ToDictionary(kv => kv.Key, kv => FromJson(kv.Value));
        }

        public static List<KeyValuePair<string, object>> FlattenNbtData(Dictionary<string, object> data)
        {
            Func<Dictionary<string, object>, IEnumerable<KeyValuePair<string, object>>> flatten = null;

            flatten = dict => dict.SelectMany(kv =>
            {
                if (kv.Value is Dictionary<string, object>)
                    return flatten((Dictionary<string, object>)kv.Value);
                if (kv.Value is Newtonsoft.Json.Linq.JObject obj)
                {
                    var res = new Dictionary<string, object>();
                    foreach (var item in obj)
                    {
                        res[item.Key] = item.Value;
                    }
                    return flatten(res);
                }
                return new List<KeyValuePair<string, object>>() { kv };
            });

            try
            {
                UnwrapList(data, "effects");
                UnwrapSouls(data);
                UnwarpStringArray(data, "ability_scroll");
                UnwarpStringArray(data, "mixins");
                UnwarpStringArray(data, "unlocked_slots");
                UnwrapJson(data, "petInfo");
                if (data.ContainsKey("petInfo"))
                    if (data["petInfo"] is Dictionary<string, object> petInfo && petInfo.ContainsKey("extraData"))
                    {
                        if (petInfo["extraData"] is Newtonsoft.Json.Linq.JObject obj)
                        {
                            foreach (var item in obj)
                            {
                                data[item.Key] = ((long)item.Value);
                            }
                        }
                        petInfo.Remove("extraData");
                    }
                UnwrapRodPart(data, "sinker");
                UnwrapRodPart(data, "line");
                UnwrapRodPart(data, "hook");
                if (data.TryGetValue("gems", out object gems))
                {
                    var type = gems.GetType();
                    if (gems is not Dictionary<string, object> dict)
                    {
                        if (gems is JObject jObject)
                        {
                            dict = jObject.ToObject<Dictionary<string, object>>();
                        }
                        else
                            dict = new();
                    }

                    var keys = dict?.Keys;
                    foreach (var item in keys)
                    {
                        if (item.EndsWith("_1") || item.EndsWith("_0") || item.EndsWith("_2") || item.EndsWith("_3") || item.EndsWith("_4"))
                        {
                            dynamic gemInfo = dict[item];
                            if (gemInfo is string)
                                continue;
                            data[item] = gemInfo["quality"];
                            data[item + ".uuid"] = gemInfo["uuid"];
                            gemInfo.Remove("uuid");
                            dict.Remove(item);
                            if (gems is JObject ob)
                            {
                                ob.Remove(item);
                            }
                        }
                    }

                }
                if (data.TryGetValue("runes", out object runesObj) && runesObj is Dictionary<string, object> runes)
                {
                    foreach (var item in runes.Keys.ToList())
                    {
                        data["RUNE_" + item] = runes[item];
                        runes.Remove(item);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e, "Error in flattening nbt data " + JSON.Stringify(data));
                throw;
            }

            var flatList = flatten(data).ToList();
            return flatList;
        }

        private static void UnwrapRodPart(Dictionary<string, object> data, string part)
        {
            if (data.TryGetValue(part, out object sinker))
            {
                if (sinker is not Dictionary<string, object> dict)
                {
                    if (sinker is JObject jObject)
                    {
                        dict = jObject.ToObject<Dictionary<string, object>>();
                    }
                    else
                        dict = new();
                }
                if (dict.TryGetValue("uuid", out object uuid))
                {
                    data[part + ".uuid"] = uuid;
                    data[part + ".part"] = dict["part"];
                    data.Remove(part);
                }
            }
        }

        private static void UnwarpStringArray(Dictionary<string, object> data, string stringArrayKey)
        {
            if (data.TryGetValue(stringArrayKey, out object abilityScroll) && abilityScroll is List<object> scrollList)
            {
                var list = scrollList
                    .Select(o => o.ToString())
                    .Select(s => s.Replace("TAG_String: ", "")
                    .Replace("\"", ""))
                    .OrderBy(a => a);
                data[stringArrayKey] = String.Join(" ", list);
            }
            else if (abilityScroll is JArray jArray)
            {
                data[stringArrayKey] = String.Join(" ", jArray.Select(a => a.ToString()));
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

        private static void UnwrapSouls(Dictionary<string, object> data)
        {

            if (!data.ContainsKey("necromancer_souls"))
                return;

            foreach (var item in (data["necromancer_souls"] as List<object>))
            {
                var kv = (item as Dictionary<string, object>);
                foreach (var keys in kv)
                {
                    var soul = keys.Value.ToString();
                    if (data.ContainsKey(soul))
                        data[soul] = (int)data[soul] + 1;
                    else
                        data[soul] = 1;
                }
            }
            data.Remove("necromancer_souls");

        }



        private static void UnwrapJson(Dictionary<string, object> data, string key)
        {
            try
            {
                if (!data.TryGetValue(key, out var content))
                    return;
                var innterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(content.ToString());
                innterData.Remove("uuid");
                data[key] = innterData;
            }
            catch (Exception e)
            {
                Console.WriteLine(e + $"\nCould not unwrap {JSON.Stringify(data[key])}");
            }
        }

        private static bool TryAs<T>(KeyValuePair<string, object> attr, out NBTLookup value) where T : IComparable, IConvertible, IFormattable
        {
            value = null;
            if (!(attr.Value is T))
                return false;
            value = new NBTLookup(Instance.GetKeyId(attr.Key), Convert.ToInt64(attr.Value));
            return true;
        }

        public static long GetColor(KeyValuePair<string, object> attr)
        {
            var fullColor = (attr.Value as string);
            return GetColor(fullColor);
        }

        public static long GetColor(string fullColor)
        {
            var parts = fullColor.Split(':');
            int result = 0;
            foreach (var item in parts)
            {
                if (!string.IsNullOrEmpty(item))
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
            try
            {
                return Convert.ToInt64(hexNum, 16);

            }
            catch (Exception)
            {
                throw new InvalidUuidException(hexNum);
            }
        }

        private static ConcurrentDictionary<string, short> Cache = new ConcurrentDictionary<string, short>();

        public short GetKeyId(string name)
        {
            return GetLookupKey(name);
        }
        public short GetLookupKey(string name)
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
                if (id != 0 || !CanWriteToDb)
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
                    Logger.Instance.Error(e, $"saving new nbtKey {name}");
                    return -1;
                }
            }, (K, v) => v);
        }
        private static ConcurrentDictionary<(short, string), int> ValueCache = new ConcurrentDictionary<(short, string), int>();
        private static readonly Dictionary<string, Tier> TierNames = new Dictionary<string, Tier>()
        {
            { "LEGENDARY", Tier.LEGENDARY },
            { "UNCOMMON", Tier.UNCOMMON },
            { "COMMON", Tier.COMMON },
            { "RARE", Tier.RARE },
            { "EPIC", Tier.EPIC },
            { "MYTHIC", Tier.MYTHIC },
            { "VERY SPECIAL", Tier.VERY_SPECIAL },
            { "SPECIAL", Tier.SPECIAL },
            { "UNKNOWN", Tier.UNKNOWN },
            { "DIVINE", Tier.DIVINE },
            { "ULTIMATE", Tier.ULTIMATE },
            { "SUPREME", Tier.DIVINE },
        };

        public NBT()
        {
        }

        public int GetValueId(short key, string value)
        {
            /*if (ValueCache.TryGetValue((key, value), out int id))
                return id;

            using (var context = new HypixelContext())
            {
                var item = context.NBTValues.Where(v => v.KeyId == key && v.Value == value).FirstOrDefault();
                if (item != null)
                    return item.Id;
            }*/

            return ValueCache.GetOrAdd((key, value), k =>
            {
                using var context = new HypixelContext();
                var id = context.NBTValues.Where(v => v.KeyId == key && v.Value == value).Select(v => v.Id).FirstOrDefault();
                if (id != 0 || !CanWriteToDb)
                    return id;
                try
                {
                    NBTValue key = AddNewValueToDb(k, context);
                    return key.Id;
                }
                catch (Exception e)
                {
                    Logger.Instance.Error(e, $"saving new nbtValue {value} for key {key}");
                    return -1;
                }
            });
        }

        protected virtual NBTValue AddNewValueToDb((short, string) k, HypixelContext context)
        {
            var key = new NBTValue() { Value = k.Item2, KeyId = k.Item1 };
            context.NBTValues.Add(key);
            context.SaveChanges();
            return key;
        }

        public static ItemReferences.Reforge GetReforge(NbtCompound f)
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

        public static IEnumerable<string> GetLore(NbtCompound rootTag)
        {
            var loreLines = rootTag
            ?.Get<NbtCompound>("tag")
            ?.Get<NbtCompound>("display")
            ?.Get<NbtList>("Lore")
            ?? rootTag?.Get<NbtCompound>("components")
            ?.Get<NbtList>("minecraft:lore");

            if (loreLines == null)
                yield break;

            foreach (var item in loreLines)
            {
                if (item.StringValue.StartsWith("{\""))
                    yield return JsonConvert.DeserializeObject<TextLine>(item.StringValue).To1_08();
                else
                    yield return item.StringValue;
            }
        }

        public static int? GetColor(NbtCompound compound)
        {
            return compound
                ?.Get<NbtCompound>("tag")
                ?.Get<NbtCompound>("display")
                ?.Get<NbtInt>("color")?.IntValue;
        }
        public static string GetName(NbtCompound rootTag)
        {
            return rootTag
            ?.Get<NbtCompound>("tag")
            ?.Get<NbtCompound>("display")
            ?.Get<NbtString>("Name")
            ?.StringValue
            ?? Get121Name(rootTag);

            static string Get121Name(NbtCompound rootTag)
            {
                var value = rootTag.Get<NbtCompound>("components")?
                            .Get<NbtString>("minecraft:custom_name")?.StringValue;
                if (value == null)
                    return null;
                return JsonConvert.DeserializeObject<TextLine>(value).To1_08();
            }
        }

        public static DateTime GetDateTime(NbtCompound file)
        {
            var extra = GetExtraTag(file);
            if (extra == null || !extra.TryGet("timestamp", out var timestamp))
            {
                return new DateTime();
            }

            if (timestamp is NbtString nbtString && DateTime.TryParseExact(
                    nbtString.StringValue,
                    new String[] { "M/d/yy h:mm tt", "M/d/yy h:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
            else if (timestamp is NbtLong nbtLong)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(nbtLong.LongValue).DateTime;
            }

            return new DateTime();
        }

        public static byte AnvilUses(NbtCompound data)
        {
            var extra = GetExtraTag(data);
            if (extra == null || !extra.TryGet<NbtInt>("anvil_uses", out NbtInt anvilUses))
            {
                return 0;
            }

            return (byte)(anvilUses.IntValue < 100 ? anvilUses.IntValue : 100);
        }

        public static short HotPotatoCount(NbtCompound data)
        {
            var extra = GetExtraTag(data);
            if (!extra.TryGet<NbtCompound>("hot_potato_count", out NbtCompound hotPotatoCount))
            {
                return 0;
            }

            return hotPotatoCount.ShortValue;
        }

        public static int Count(NbtCompound rootTag)
        {
            return rootTag
                ?.Get<NbtByte>("Count")?.ByteValue
                ?? rootTag
                ?.Get<NbtInt>("count")?.IntValue ?? 0;
        }

        public static NbtCompound GetExtraTag(NbtCompound rootTag)
        {
            return rootTag
                ?.Get<NbtCompound>("tag")
                ?.Get<NbtCompound>("ExtraAttributes")
                ?? rootTag.Get<NbtCompound>("components")? // 1.21 position
                .Get<NbtCompound>("minecraft:custom_data");
        }

        public static Dictionary<string, byte> GetEnchants(NbtCompound data)
        {
            var extra = GetExtraTag(data);
            if (extra == null || !extra.TryGet<NbtCompound>("enchantments", out NbtCompound elements))
            {
                return null;
            }
            return elements.ToDictionary(e => e.Name, e => (byte)Math.Min(e.IntValue, 127));
        }

        public static List<Enchantment> Enchantments(NbtCompound data)
        {
            var extra = GetExtraTag(data);
            if (extra == null || !extra.TryGet<NbtCompound>("enchantments", out NbtCompound elements))
            {
                return new List<Enchantment>();
            }

            var result = new List<Enchantment>();

            foreach (var item in elements.Names)
            {
                if (Constants.AttributeKeys.Contains(item))
                    continue; // for some reason they are now added into enchants sometimes
                if (!Enum.TryParse(item, true, out Enchantment.EnchantmentType type))
                {
                    if (!Enum.TryParse("ultimate_" + item, true, out type))
                        Logger.Instance.Error("Did not find Enchantment " + item + " in " + extra.ToString());
                }
                var level = elements.Get<NbtInt>(item).IntValue;
                result.Add(new Enchantment(type, (byte)level));
            }

            return result;
        }

        public static string ItemID(string input)
        {
            var f = File(Convert.FromBase64String(input));

            return ItemID(f.RootTag);
        }

        public static string ItemID(NbtCompound file)
        {
            var nbt = GetExtraTag(file);
            return ItemIdFromExtra(nbt);

        }

        public static string ItemIdFromExtra(NbtCompound nbt)
        {
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
            else if (id?.EndsWith("RUNE") ?? false)
            {
                var tag = nbt.Get<NbtCompound>("runes");
                var runeType = tag?.Tags?.First().Name;
                id += $"_{runeType}";
            }
            else if (id == "ABICASE")
            {
                if (nbt.TryGet<NbtString>("model", out NbtString model))
                    id += $"_{model.StringValue}";
            }

            return id;
        }

        private static string GetPetId(NbtCompound nbt, string id)
        {
            try
            {
                if (nbt.TryGet("petInfo", out NbtTag petInfo) && petInfo.TagType == NbtTagType.String)
                {
                    PetInfo info = JsonConvert.DeserializeObject<PetInfo>(petInfo.StringValue);
                    var petType = info.Type;
                    id += $"_{petType}";
                    return id;
                }
                var compound = nbt.Get<NbtCompound>("petInfo");
                var type = compound.Get<NbtString>("type");
                id += $"_{type.StringValue}";
                Console.WriteLine("Pet type extracted " + type.StringValue);
                return id;
            }
            catch (Exception e)
            {
                var base64 = Convert.ToBase64String(Bytes(nbt));
                Logger.Instance.Error(e, $"Could not get itemId from nbt {nbt?.ToString()}\n{base64}");
                return "PET_unkown";
            }
        }

        public static byte[] Extra(string input)
        {
            var f = File(Convert.FromBase64String(input));

            var a = f.ToString();
            return Extra(f.RootTag);
        }

        public static NbtCompound GetReducedExtra(NbtCompound file)
        {
            var tag = GetExtraTag(file);
            if (tag == null)
                return null;

            tag.Remove("enchantments");
            tag.Remove("id");
            tag.Remove("originTag");

            if (tag.Contains("uuid"))
            {
                var uuid = tag.Get<NbtString>("uuid");
                tag.Add(new NbtString("uid", uuid.StringValue.Substring(24)));
            }

            return tag;

        }

        public static byte[] Extra(NbtCompound file)
        {
            var tag = GetReducedExtra(file);
            if (tag == null)
                return null;

            tag.Remove("modifier");
            tag.Remove("hotPotatoBonus");
            tag.Remove("originTag");
            tag.Remove("timestamp");
            tag.Remove("anvil_uses");

            if (tag.Contains("hot_potato_count"))
            {
                // rename to be smaler
                var count = tag.Get<NbtInt>("hot_potato_count");
                count.Name = "hpc";
            }

            tag.Name = "";

            return Bytes(tag);
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
                f.LoadFromBuffer(input, 0, input.Length, compression);
            }

            return f;
        }

        public static byte[] Bytes(NbtFile file)
        {
            var outStream = new MemoryStream();
            file.SaveToStream(outStream, NbtCompression.None);
            return outStream.ToArray();
        }
        public static byte[] Bytes(NbtCompound root)
        {
            var file = new NbtFile(root);
            using var outStream = new MemoryStream();
            file.SaveToStream(outStream, NbtCompression.None);
            return outStream.ToArray();
        }
    }

    public class TextElement
    {
        public string Text { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string Color { get; set; }
    }
    public class TextLine
    {
        private static Dictionary<string, string> colorList;
        static TextLine()
        {
            colorList = typeof(McColorCodes)
              .GetFields(BindingFlags.Public | BindingFlags.Static)
              .Where(f => f.FieldType == typeof(string))
              .ToDictionary(f => f.Name.ToLower(),
                            f => (string)f.GetValue(null));
        }
        public List<TextElement> Extra { get; set; }

        public string To1_08()
        {
            if (Extra == null)
                return string.Empty;
            return string.Join("", Extra.Select(e => $"{(e.Bold ? McColorCodes.BOLD : String.Empty)}{(e.Italic ? McColorCodes.ITALIC : String.Empty)}{(e.Color != null && colorList.TryGetValue(e.Color, out var c) ? c : String.Empty)}{e.Text}"));
        }
    }
}