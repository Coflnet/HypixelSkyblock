using System;
using System.Text;
using System.Linq;
using System.Web;

namespace hypixel
{
    public class HtmlModifier
    {
        public static string ModifyContent(string path, byte[] contents)
        {
            var defaultText = "Browse over 100 million auctions, and the bazzar of Hypixel SkyBlock";
            var defaultTitle = "Skyblock Auction House History";
            string parameter = "";
            if (path.Split('/', '?', '#').Length > 2)
                parameter = path.Split('/', '?', '#')[2];
            string description = defaultText;
            string title = defaultTitle;
            string imageUrl = "https://sky.coflnet.com/logo192.png";
            string keyword = "";

            string html = Encoding.UTF8.GetString(contents);

            // try to fill in title
            if (path.Contains("auction/") || path.Contains("a/"))
            {
                // is an auction
                using (var context = new HypixelContext())
                {
                    var result = context.Auctions.Where(a => a.Uuid == parameter)
                            .Select(a => new { a.Tag, a.AuctioneerId, a.ItemName, a.End, bidCount = a.Bids.Count, a.Tier, a.Category }).FirstOrDefault();
                    if (result != null)
                    {
                        var playerName = PlayerSearch.Instance.GetNameWithCache(result.AuctioneerId);
                        title = $"Auction for {result.ItemName} by {playerName}";
                        description = $"{title} ended on {result.End} with {result.bidCount} bids, Category: {result.Category}, {result.Tier}";
                        keyword = $"{result.ItemName},{playerName}";

                        if (!string.IsNullOrEmpty(result.Tag))
                            imageUrl = "https://sky.lea.moe/item/" + result.Tag;
                        else
                            imageUrl = "https://crafatar.com/avatars/" + result.AuctioneerId;

                    }
                }
            }
            if (path.Contains("player/") || path.Contains("p/"))
            {
                if (parameter.Length < 30)
                {
                    var uuid = PlayerSearch.Instance.GetIdForName(parameter);
                    return Redirect(uuid, "player", parameter);
                }
                keyword = PlayerSearch.Instance.GetNameWithCache(parameter);
                if(!path.Contains(keyword))
                    path += $"/{keyword}";
                title = $"{keyword} Auctions and bids";
                description = $"Auctions and bids for {keyword}. Recent Auctions, bids, and prices for hypixel SkyBlock auctionhouse and bazaar history with filters.";
                imageUrl = "https://crafatar.com/avatars/" + parameter;
            }
            if (path.Contains("item/") || path.Contains("i/"))
            {
                if (path.Contains("i/"))
                    return AddItemRedirect(parameter, keyword);
                if (parameter.ToUpper() != parameter && !parameter.StartsWith("POTION"))
                {
                    // likely not a tag
                    parameter = HttpUtility.UrlDecode(parameter);
                    var thread = ItemDetails.Instance.Search(parameter, 1);
                    thread.Wait();
                    var item = thread.Result.FirstOrDefault();
                    keyword = item?.Name;
                    parameter = item?.Tag;
                    return AddItemRedirect(parameter, keyword);
                }
                else
                {
                    keyword = ItemDetails.TagToName(parameter);
                }

                var i = ItemDetails.Instance.GetDetailsWithCache(parameter);
                if(!path.Contains(keyword))
                    path += $"/{keyword}";
                title = $"{keyword} price ";
                description = $"Price for item {keyword} in hypixel SkyBlock"
                + AddAlternativeNames(i);
                imageUrl = "https://sky.lea.moe/item/" + parameter;
            }
            title += " | Hypixel SkyBlock Auction house history tracker";
            var longDescription = description;
            // shrink to under 70 chars
            while (title.Length > 70)
            {
                title = title.Substring(0, title.LastIndexOf(' '));
            }
            if(path == "/index.html")
            {
                path = "";
            }

            var newHtml = html
                        .Replace(defaultText, description)
                        .Replace(defaultTitle, title)
                        .Replace("</title>", $"</title><meta property=\"keywords\" content=\"{keyword},hypixel,skyblock,auction,history,bazaar,tracker\" /><meta property=\"og:image\" content=\"{imageUrl}\" />"
                            + $"<link rel=\"canonical\" href=\"https://sky.coflnet.com/{path}\" />")
                        .Replace("</body>", PopularPages(title, description) + "</body>");
            return newHtml;
        }

        private static string AddAlternativeNames(DBItem i)
        {
            return ". Found names: " + i.Names.Select(n => n.Name).Aggregate((a, b) => $"{a}, {b}").TrimEnd(' ', ',');
        }

        private static string AddItemRedirect(string parameter, string name)
        {
            return Redirect(parameter, "item", name);
        }

        private static string Redirect(string parameter, string type, string seoTerm = null)
        {
            return $"https://sky.coflnet.com/{type}/{parameter}" + seoTerm == null ? "" : $"/{seoTerm}";
        }

        private static string PopularPages(string title, string description)
        {
            var r = new Random();
            var recentSearches = SearchService.Instance.GetPopularSites().OrderBy(x => r.Next());
            var body = $@"<div style=""display: none;"">
                    <h1>{title}</h1><p>{description}</p><p>View, search, browse, and filter by reforge or enchantment. "
                    + "You can find all current and historic prices for the auction house and bazaar on this web tracker. "
                    + "We are tracking about 175 million auctions. "
                    + "Saved more than 230 million bazaar prices in intervalls of 10 seconds. "
                    + "Furthermore there are over two million <a href=\"/players\"> skyblock players</a> that you can search by name and browse through the auctions they made over the past two years. "
                    + "The autocomplete search is ranked by popularity and allows you to find whatever <a href=\"/items\">item</a> you want faster. "
                    + "New Items are added automatically and available within two miniutes after the first auction is startet. "
                    + "We allow you to subscribe to auctions, item prices and being outbid with more to come. "
                    + "Quick urls allow you to link to specific sites. /p/Steve or /i/Oak allow you to create a link without visiting the site. ";
            if (recentSearches.Any())
                body += "<h2>Other Players and item auctions:</h2>"
                    + recentSearches
                    .Take(8)
                .Select(p => $"<a href=\"https://sky.coflnet.com/{p.Url}\">{p.Title} </a>")
                .Aggregate((a, b) => a + b) + "</div>";
            return body;
        }
    }
}
