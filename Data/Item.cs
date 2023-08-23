using System.Collections.Generic;
using MessagePack;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core;
#nullable enable
[MessagePackObject]
public class Item
{
    /// <summary>
    /// 
    /// </summary>
    [Key(0)]
    public long? Id { get; set; }
    /// <summary>
    /// The item name for display
    /// </summary>
    [Key(1)]
    public string ItemName { get; set; } = null!;
    /// <summary>
    /// Hypixel item tag for this item
    /// </summary>
    [Key(2)]
    public string Tag { get; set; } = null!;
    /// <summary>
    /// Other aditional attributes
    /// </summary>
    [Key(3)]
    public Dictionary<string, object>? ExtraAttributes { get; set; }

    /// <summary>
    /// Enchantments if any
    /// </summary>
    [Key(4)]
    public Dictionary<string, byte>? Enchantments { get; set; } = new();
    /// <summary>
    /// Color element
    /// </summary>
    [Key(5)]
    public int? Color { get; set; }
    /// <summary>
    /// Item Description aka Lore displayed in game, is a written form of <see cref="ExtraAttributes"/>
    /// </summary>
    [Key(6)]
    public string? Description { get; set; }
    /// <summary>
    /// Stacksize
    /// </summary>
    [Key(7)]
    public byte Count { get; set; }

}
#nullable restore


public partial class ItemDetails
{
    [MessagePackObject]
    public class Item : IItem
    {
        public static Item Default = new Item() { Id = "unknown", Description = "This item has not yet been reviewed by our team" };

        [Key(0)]
        public string Id;
        [NotMapped]
        [Key(1)]
        public HashSet<string> AltNames = new HashSet<string>();
        [Key(2)]
        public string Description;
        [Key(3)]
        public string IconUrl { get; set; }

        [Key(4)]
        public string Category;
        [Key(5)]
        public string Extra;
        [Key(6)]
        public string Tier;
        [Key(7)]
        public string MinecraftType { get; set; }

        [Key(8)]
        public string color;

    }

    public class ItemSearchResult
    {
        [Key(0)]
        public string Name;

        [Key(1)]
        public string Tag;

        [Key(2)]
        public string IconUrl;

        [Key(3)]
        public int HitCount;
        [Key(4)]
        public Tier Tier;
    }

    public async Task<IEnumerable<ItemSearchResult>> Search(string search, int count = 5)
    {
        var clearedSearch = ItemReferences.RemoveReforgesAndLevel(search).TrimStart().TrimEnd();
        var tagified = search.ToUpper().Replace(' ', '_');
        if (tagified.EndsWith("_pet"))
            tagified = "PET_" + tagified.Replace("_pet", "");
        using (var context = new HypixelContext())
        {
            var items = await context.Items
                .Include(item => item.Names)
                .Where(item =>
                    item.Names
                    .Where(name => EF.Functions.Like(name.Name, clearedSearch + '%')
                    || EF.Functions.Like(name.Name, "Enchanted " + clearedSearch + '%')
                    || EF.Functions.Like(name.Name, search + '%')).Any()
                    || EF.Functions.Like(item.Tag, tagified + '%')
                ).OrderBy(item => item.Name.Length / 2 - item.HitCount - (item.Name == clearedSearch ? 10000000 : 0))
                .Take(count)
                .ToListAsync();

            return ToSearchResult(items, clearedSearch);
        }
    }

    public async Task<IEnumerable<DBItem>> GetBazaarItems()
    {
        using (var context = new HypixelContext())
        {
            return await context.Items
                .Where(i => i.IsBazaar)
                .ToListAsync();

        }
    }

    private static IEnumerable<ItemSearchResult> ToSearchResult(List<DBItem> items, string search)
    {

        var clearedSearch = ItemReferences.RemoveReforgesAndLevel(search);
        return items
            .Select(item => new ItemSearchResult()
            {
                Name = (item.Names
                        .Where(n => n?.Name != null && n.Name.ToLower().StartsWith(clearedSearch.ToLower())
                            && n.Name != "Beastmaster Crest" && n.Name != "Griffin Upgrade Stone")
                        .FirstOrDefault()?.Name) ?? (item.Name == item.Tag ? TagToName(item.Tag) : item.Name),
                Tag = item.Tag,
                IconUrl = item.IconUrl,
                HitCount = item.HitCount,
                Tier = item.Tier
            });
    }

    /// <summary>
    /// Finds the item(s) with the closest name.
    /// Switched, adds and removes characters to do so
    /// </summary>
    /// <param name="search"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public async Task<IEnumerable<ItemSearchResult>> FindClosest(string search, int count = 5)
    {
        if (search.Length <= 3 || search.Length > 16)
            return new ItemSearchResult[0];

        var possibleCorrect = new List<string>();

        // typed a wrong character is included in switched two
        // switched two
        for (int i = 2; i < search.Length; i++)
        {
            StringBuilder sb = new StringBuilder(search);
            sb[i] = '_';
            sb[i - 1] = '_';
            var guess = sb.ToString();
            possibleCorrect.Add(guess);
        }
        // missed a char
        for (int i = 1; i < search.Length; i++)
        {
            StringBuilder sb = new StringBuilder(search);
            sb.Insert(i, '_');
            var guess = sb.ToString();
            possibleCorrect.Add(guess);
        }

        // a char to much 
        for (int i = 1; i < search.Length; i++)
        {
            StringBuilder sb = new StringBuilder(search);
            sb.Remove(i, 1);
            var guess = sb.ToString();
            possibleCorrect.Add(guess);
        }
        Console.WriteLine($"Matching total of {possibleCorrect.Count()} possible corrections");

        using (var context = new HypixelContext())
        {
            foreach (var placeHolderValue in possibleCorrect)
            {
                var items = await context.Items
                    .Include(item => item.Names)
                    .Where(item =>
                        item.Names
                        .Where(name => EF.Functions.Like(name.Name, placeHolderValue + '%')).Any()
                    ).OrderBy(item => item.HitCount)
                    .Take(count)
                    .ToListAsync();
                if (items.Count() > 0)
                    return ToSearchResult(items, search);
            }
        }
        return new ItemSearchResult[0];
    }

    internal int GetOrCreateItemByTag(string tag)
    {
        var id = GetItemIdForTag(tag, false);
        if (id != 0)
            return id;

        throw new Exception("item creation is handled by the item service now");
        using (var context = new HypixelContext())
        {
            id = context.Items.Where(i => i.Tag == tag).Select(i => i.Id).FirstOrDefault();
            if (id != 0)
            {
                TagLookup[tag] = id;
                return id;
            }
        }
        Console.WriteLine($"Adding Tag {tag}");
        var name = TagToName(tag);
        var newItem = new DBItem()
        {
            Tag = tag,
            Name = name,
            Names = new List<AlternativeName>()
                        {new AlternativeName(){Name=name}}
        };

        return AddItemToDB(newItem);

    }

    public static string TagToName(string tag)
    {
        if (tag == null || tag.Length <= 2)
            return tag;
        var split = tag.ToLower().Split('_');
        var result = "";
        foreach (var item in split)
        {
            if (item == "of" || item == "the")
                result += " " + item;
            else
                result += " " + Char.ToUpper(item[0]) + item.Substring(1);
        }
        return result.Trim();
    }

    private const int MAX_MEDIUM_INT = 8388607;

    public int GetOrCreateItemIdForAuction(SaveAuction auction, HypixelContext context)
    {
        var clearedName = ItemReferences.RemoveReforgesAndLevel(auction.ItemName);
        var id = GetItemIdForTag(auction.Tag ?? clearedName);
        return id;

        Console.WriteLine($"Creating item {clearedName} ({auction.ItemName},{auction.Tag})");
        // doesn't exist yet, create it
        var itemByTag = context.Items.Where(item => item.Tag == auction.Tag).FirstOrDefault();
        if (itemByTag != null)
        {
            // new alternative name
            if (clearedName != null)
                ReverseNames[clearedName] = auction.Tag;
            TagLookup.TryAdd(auction.Tag, itemByTag.Id);
            var exists = context.AltItemNames
                .Where(name => name.Name == clearedName && name.DBItemId == itemByTag.Id)
                .Any();
            if (!exists)
                context.AltItemNames.Add(new AlternativeName() { DBItemId = itemByTag.Id, Name = clearedName });
            return itemByTag.Id;
        }
        Console.WriteLine($"!! completely new !! {JsonConvert.SerializeObject(auction)}");
        var item = new DBItem()
        {
            Tag = auction.Tag,
            Name = auction.ItemName,
            Names = new List<AlternativeName>() { new AlternativeName() { Name = auction.ItemName } }
        };
        if (item.Tag == null)
        {
            // unindexable item
            return MAX_MEDIUM_INT;
        }
        // todo send this to an updater
        //ToFillDetails[item.Tag] = item;
        return AddItemToDB(item);
        //throw new CoflnetException("can_add","can't add this item");
    }
}

