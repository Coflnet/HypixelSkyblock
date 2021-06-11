using System;
using System.Text;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using static hypixel.ItemPrices;

namespace hypixel
{
    public class HtmlModifier
    {

        const string defaultText = "Browse over 100 million auctions, and the bazzar of Hypixel SkyBlock";
        const string defaultTitle = "Skyblock Auction House History";
        const string DETAILS_START = @"<noscript>";
        public static async Task<string> ModifyContent(string path, byte[] contents, Server.RequestContext res)
        {
            string parameter = "";
            var urlParts = path.Split('/', '?', '#');
            if (urlParts.Length > 2)
                parameter = urlParts[2];
            string description = "Browse over 200 million auctions, and the bazaar of Hypixel SkyBlock.";
            string longDescription = null;
            string title = defaultTitle;
            string imageUrl = "https://sky.coflnet.com/logo192.png";
            string keyword = "";

            var start = Encoding.UTF8.GetString(contents).Split("<title>");
            var headerStart = start[0] + "<title>";
            var parts = start[1].Split("</head>");
            string header = parts.First();
            string html = parts.Last().Substring(0,parts.Last().Length - 14);


            if(path.StartsWith("/p/"))
                return res.RedirectSkyblock(parameter, "player");
            if(path.StartsWith("/a/"))
                return res.RedirectSkyblock(parameter, "auction");
            if(path == "/item/" || path == "/item")
                return res.RedirectSkyblock();

            // try to fill in title
            if (path.Contains("auction/"))
            {
                await WriteStart(res,headerStart);
                // is an auction
                using (var context = new HypixelContext())
                {
                    var result = context.Auctions.Where(a => a.Uuid == parameter)
                            .Select(a => new { a.Tag, a.AuctioneerId, a.ItemName, a.End, bidCount = a.Bids.Count, a.Tier, a.Category }).FirstOrDefault();
                    if (result == null)
                    {
                        await WriteHeader("/error",res,"This site was not found","Error",imageUrl,null,header);
                        await res.WriteEnd(html);
                        return "";
                    }
                    

                        var playerName = PlayerSearch.Instance.GetNameWithCache(result.AuctioneerId);
                        title = $"Auction for {result.ItemName} by {playerName}";
                        description = $"{title} ended on {result.End.ToString("yyyy-MM-dd HH\\:mm\\:ss")} with {result.bidCount} bids, Category: {result.Category}, {result.Tier}.";


                        if (!string.IsNullOrEmpty(result.Tag))
                            imageUrl = "https://sky.lea.moe/item/" + result.Tag;
                        else
                            imageUrl = "https://crafatar.com/avatars/" + result.AuctioneerId;

                        await WriteHeader(path, res, description, title, imageUrl, keyword, header);

                        longDescription = description
                            + $"<ul><li> <a href=\"/player/{result.AuctioneerId}/{playerName}\"> other auctions by {playerName} </a></li>"
                            + $" <li><a href=\"/item/{result.Tag}/{result.ItemName}\"> more auctions for {result.ItemName} </a></li></ul>";
                        keyword = $"{result.ItemName},{playerName}";


                    
                }
            } else if (path.Contains("player/"))
            {
                if (parameter.Length < 30)
                {
                    var uuid = PlayerSearch.Instance.GetIdForName(parameter);
                    return res.RedirectSkyblock(uuid, "player", parameter);
                }

                await WriteStart(res,headerStart);
                keyword = PlayerSearch.Instance.GetNameWithCache(parameter);
                if (urlParts.Length <= 3)
                    path += $"/{keyword}";
                title = $"{keyword} Auctions and bids";
                description = $"Auctions and bids for {keyword} in hypixel skyblock.";

                imageUrl = "https://crafatar.com/avatars/" + parameter;

                await WriteHeader(path, res, description, title, imageUrl, keyword, header);


                var auctions = GetAuctions(parameter, keyword);
                var bids = GetBids(parameter, keyword);
                await res.WriteAsync(html);
                await res.WriteAsync(DETAILS_START + $"<h1>{title}</h1>{description} " + await auctions);
                await res.WriteEnd(await bids + PopularPages());

                return "";
            } else if (path.Contains("item/") || path.Contains("i/"))
            {
                if (path.Contains("i/"))
                    return res.RedirectSkyblock(parameter, "item", keyword);
                if (!ItemDetails.Instance.TagLookup.ContainsKey(parameter) )
                {
                    var upperCased = parameter.ToUpper();
                    if(ItemDetails.Instance.TagLookup.ContainsKey(upperCased))
                        return res.RedirectSkyblock(upperCased, "item");
                    // likely not a tag
                    parameter = HttpUtility.UrlDecode(parameter);
                    var thread = ItemDetails.Instance.Search(parameter, 1);
                    thread.Wait();
                    var item = thread.Result.FirstOrDefault();
                    keyword = item?.Name;
                    parameter = item?.Tag;
                    return res.RedirectSkyblock(parameter, "item", keyword);
                }
                await WriteStart(res,headerStart);
                keyword = ItemDetails.TagToName(parameter);
                

                var i = await ItemDetails.Instance.GetDetailsWithCache(parameter);
                path = CreateCanoicalPath(urlParts, i);

                title = $"{keyword} price ";
                float price = await GetAvgPrice(parameter);
                description = $"Price for item {keyword} in hypixel SkyBlock is {price.ToString("0,0.0")} on average. Visit for a nice chart and filter options";
                imageUrl = "https://sky.lea.moe/item/" + parameter;
                if(parameter.StartsWith("PET_") && !parameter.StartsWith("PET_ITEM") || parameter.StartsWith("POTION"))
                    imageUrl = i.IconUrl;
                await WriteHeader(path, res, description, title, imageUrl, keyword, header);

                longDescription = description
                + AddAlternativeNames(i);

                longDescription += await GetRecentAuctions(i.Tag == "Unknown" || i.Tag == null ? parameter : i.Tag);
            }
            else {
                // unkown site, write the header
                await WriteStart(res,headerStart);
                await WriteHeader(path, res, description, "", imageUrl, keyword, header);
            }
            if (longDescription == null)
                longDescription = description;


            var newHtml = html + DETAILS_START
                        + BottomText(title, longDescription) ;

            await res.WriteEnd(newHtml);
            return newHtml;
        }

        private static async Task<float> GetAvgPrice(string tag)
        {
            try 
            {
            var prices = (await ItemPrices.Instance.GetPriceFor(new ItemSearchQuery() { name = tag, Start = DateTime.Now - TimeSpan.FromDays(1) })).Prices;
            if(prices == null || prices.Count == 0)
                return 0;
            return prices.Average(a => a.Avg);
            } catch (Exception e)
            {
                Console.WriteLine($"Could not get price for {tag} {e.Message} {e.StackTrace}");
                return -1;
            }

        }

        private static async Task WriteStart(Server.RequestContext res, string content)
        {
            await res.WriteAsync(content);
           // res.SendChunked = true;
            res.AddHeader("cache-control", "public,max-age=" + 1800);

            res.ForceSend();
        }

        private static async Task WriteHeader(string path, Server.RequestContext res, string description, string title, string imageUrl, string keyword, string header)
        {
            title += " Hypixel SkyBlock Auction house history tracker";
            // shrink to fit
            while (title.Length > 65)
            {
                title = title.Substring(0, title.LastIndexOf(' '));
            }
            if (path == "/index.html")
            {
                path = "";
            }


            await res.WriteAsync(header
            .Replace(defaultText, description)
            .Replace(defaultTitle, title)
            .Replace("</title>", $"</title><meta property=\"keywords\" content=\"{keyword},hypixel,skyblock,auction,history,bazaar,tracker\" />"
                + $"<meta property=\"og:image\" content=\"{imageUrl}\" />"
                + $"<meta property=\"og:url\" content=\"https://sky.coflnet.com{path}\" />"
                + $"<meta property=\"og:title\" content=\"{title}\" />"
                + $"<meta property=\"og:description\" content=\"{description}\" />"
                + $"<link rel=\"canonical\" href=\"https://sky.coflnet.com{path}\" />"
                )
                + "</head>");
            
            res.ForceSend();
        }

        private static string CreateCanoicalPath(string[] urlParts, DBItem i)
        {
            return $"/item/{i.Tag}" + (urlParts.Length > 3 ? $"/{ItemReferences.RemoveReforgesAndLevel(HttpUtility.UrlDecode(urlParts[3])) }" : "");
        }

        private static async Task<string> GetBids(string parameter, string name)
        {
            var bidsTask = Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<PlayerBidsCommand.BidResult>.Request,
            List<PlayerBidsCommand.BidResult>>("playerBids", new PaginatedRequestCommand<PlayerBidsCommand.BidResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = parameter });

            var sb = new StringBuilder();
            var bids = await bidsTask;

            sb.Append("<h2>Bids</h2> <ul>");
            foreach (var item in bids)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");

            var auctionAndBids = sb.ToString();
            return auctionAndBids;
        }

        private static async Task<string> GetAuctions(string uuid, string name)
        {
            var auctions = await Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>.Request,
            List<PlayerAuctionsCommand.AuctionResult>>("playerAuctions", new PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = uuid });

            var sb = new StringBuilder();

            sb.Append($"<h2>{name} Auctions</h2> <ul>");
            foreach (var item in auctions)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static async Task<string> GetRecentAuctions(string tag)
        {
            if(tag == null)
                return "";
            var isBazaar = ItemPrices.Instance.IsBazaar(ItemDetails.Instance.GetItemIdForName(tag));
            if(isBazaar)
                return " This is a bazaar item. Bazaartracker.com currently gives you a more detailed view of this history. ";
            var result = await Server.ExecuteCommandWithCache<ItemSearchQuery, IEnumerable<AuctionPreview>>("recentAuctions", new ItemSearchQuery()
            {
                name = tag,
                Start = DateTime.Now.Subtract(TimeSpan.FromHours(3)).RoundDown(TimeSpan.FromMinutes(30))
            });
            var sb = new StringBuilder(200);
            sb.Append("<br>Recent auctions: <ul>");
            foreach (var item in result)
            {
                sb.Append($"<li><a href=\"/auction/{item.Uuid}\">auction by {PlayerSearch.Instance.GetNameWithCache(item.Seller)}</a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string AddAlternativeNames(DBItem i)
        {
            if (i.Names == null || i.Names.Count == 0)
                return "";
            return ". Found this item with the following names: " + i.Names.Select(n => n.Name).Aggregate((a, b) => $"{a}, {b}").TrimEnd(' ', ',')
            + ". This are all names under wich we found auctins for this item in the ah. It may be historical names or names in a different language.";
        }

        private static string BottomText(string title, string description)
        {
            return $@"<h1>{title}</h1><p>{description}</p>"
                    + PopularPages();
        }

        private static string PopularPages()
        {
            var r = new Random();
            var recentSearches = SearchService.Instance.GetPopularSites().OrderBy(x => r.Next());
            var body = "<h2>Description</h2><p>View, search, browse, and filter by reforge or enchantment. "
                    + "You can find all current and historic prices for the auction house and bazaar on this web tracker. "
                    + "We are tracking about 200 million auctions. "
                    + "Saved more than 250 million bazaar prices in intervalls of 10 seconds. "
                    + "Furthermore there are over two million <a href=\"/players\"> skyblock players</a> that you can search by name and browse through the auctions they made over the past two years. "
                    + "The autocomplete search is ranked by popularity and allows you to find whatever <a href=\"/items\">item</a> you want faster. "
                    + "New Items are added automatically and available within two miniutes after the first auction is startet. "
                    + "We allow you to subscribe to auctions, item prices and being outbid with more to come. "
                    + "Quick urls allow you to link to specific sites. /p/Steve or /i/Oak allow you to create a link without visiting the site first. "
                    + "Please use the contact on the Feedback site to send us suggestions or bug reports. </p>";
            if (recentSearches.Any())
                body += "<h2>Other Players and item auctions:</h2>"
                    + recentSearches
                    .Take(8)
                .Select(p => $"<a href=\"https://sky.coflnet.com/{p.Url}\">{p.Title} </a>")
                .Aggregate((a, b) => a + b);
            return body + "</noscript>";
        }
    }
}
