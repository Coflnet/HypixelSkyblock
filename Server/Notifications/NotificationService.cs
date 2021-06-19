using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RestSharp;

namespace hypixel
{
    /// <summary>
    /// Sends firebase push notifications
    /// </summary>
    public partial class NotificationService
    {
        public static NotificationService Instance { get; set; }

        public static string BaseUrl = "https://sky.coflnet.com";
        public static string ItemIconsBase = "https://sky.lea.moe/item";

        static NotificationService()
        {
            Instance = new NotificationService();
        }

        internal void AddToken(int userId, string deviceName, string token)
        {
            using (var context = new HypixelContext())
            {
                var user = context.Users.Where(u => u.Id == userId).Include(u => u.Devices).FirstOrDefault();
                if (user == null)
                {
                    throw new CoflnetException("unknown_user", "The user is not known");
                }
                var target = user.Devices.Where(d => d.Name == deviceName);
                if (target.Any())
                {
                    var device = target.First();
                    device.Token = token;
                    context.Update(device);
                }
                else
                {
                    var hasPremium = user.PremiumExpires > DateTime.Now;
                    if (!hasPremium && user.Devices.Count >= 3)
                        throw new CoflnetException("no_premium", "You need premium to add more than 3 devices");
                    if (user.Devices.Count > 10)
                        throw new CoflnetException("limit_reached", "You can't have more than 11 devices linked to your account");
                    var device = new Device() { UserId = user.Id, Name = deviceName, Token = token };
                    user.Devices.Add(device);
                    context.Update(user);
                    context.Add(device);
                }
                context.SaveChanges();

            }
        }

        DoubleNotificationPreventer doubleChecker = new DoubleNotificationPreventer();

        internal async Task Send(int userId, string title, string text, string url, string icon, object data = null)
        {
            var not = new Notification(title, text, url, icon, null, data);
            if (!doubleChecker.HasNeverBeenSeen(userId, not))
                return;

            try
            {
                using (var context = new HypixelContext())
                {
                    var devices = context.Users.Where(u => u.Id == userId).SelectMany(u => u.Devices);
                    foreach (var item in devices)
                    {
                        Console.WriteLine("sending to " + item.UserId);
                        var success = await TryNotifyAsync(item.Token, not);
                        if (success)
                            return;
                        dev.Logger.Instance.Error("Sending pushnotification failed to");
                        dev.Logger.Instance.Error(JsonConvert.SerializeObject(item));
                        context.Remove(item);
                    }
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                dev.Logger.Instance.Error($"Could not send {not.body} to {userId}");
            }

        }

        /// <summary>
        /// Attempts to send a notification
        /// </summary>
        /// <param name="to"></param>
        /// <param name="notification"></param>
        /// <returns><c>true</c> when the notification was sent successfully</returns>
        public async Task<bool> TryNotifyAsync(string to, Notification notification)
        {
            try
            {
                // Get the server key from FCM console
                var serverKey = string.Format("key={0}", SimplerConfig.Config.Instance["firebaseKey"]);

                // Get the sender id from FCM console
                var senderId = string.Format("id={0}", SimplerConfig.Config.Instance["firebaseSenderId"]);

                //var icon = "https://sky.coflnet.com/logo192.png";
                var data = notification.data;
                var payload = new
                {
                    to, // Recipient device token
                    notification,
                    data
                };

                // Using Newtonsoft.Json
                var jsonBody = JsonConvert.SerializeObject(payload);

                /*       var client = new RestClient();
                       var request = new RestRequest("https://fcm.googleapis.com/fcm/send", Method.POST);
                       Console.WriteLine("y");
                       request.AddHeader("Authorization", serverKey);
                       request.AddHeader("Sender", senderId);
                       request.AddJsonBody(payload);
                       Console.WriteLine(jsonBody);
                       Console.WriteLine(serverKey);
                      //Console.WriteLine(JsonConvert.SerializeObject(request));

                       var response = await client.ExecuteAsync(request);*/

                var client = new RestClient("https://fcm.googleapis.com/fcm/send");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);

                request.AddHeader("Authorization", serverKey);
                request.AddHeader("Sender", senderId); request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", jsonBody, ParameterType.RequestBody);
                IRestResponse response = await client.ExecuteAsync(request);


                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(response));
                }

                dynamic res = JsonConvert.DeserializeObject(response.Content);
                var success = res.success == 1;
                if(!success)
                    dev.Logger.Instance.Error(response.Content);

                return success;
            }
            catch (Exception ex)
            {
                dev.Logger.Instance.Error($"Exception thrown in Notify Service: {ex.Message} {ex.StackTrace}");
            }
            Console.WriteLine("done");

            return false;
        }

        internal void Sold(SubscribeItem sub, SaveAuction auction)
        {
            var text = $"{auction.ItemName} was sold to {PlayerSearch.Instance.GetNameWithCache(auction.Bids.FirstOrDefault().Bidder)} for {auction.HighestBidAmount}";
            Task.Run(() => Send(sub.UserId, "Item Sold", text, AuctionUrl(auction), ItemIconUrl(auction.Tag), FormatAuction(auction)));
        }

        public void Outbid(SubscribeItem sub, SaveAuction auction, SaveBids bid)
        {
            var outBidBy = auction.HighestBidAmount - bid.Amount;
            var text = $"You were outbid on {auction.ItemName} by {PlayerSearch.Instance.GetNameWithCache(auction.Bids.FirstOrDefault().Bidder)} by {outBidBy}";
            Task.Run(() => Send(sub.UserId, "Outbid", text, AuctionUrl(auction), ItemIconUrl(auction.Tag), FormatAuction(auction)));
        }

        public void NewBid(SubscribeItem sub, SaveAuction auction, SaveBids bid)
        {
            var text = $"New bid on {auction.ItemName} by {PlayerSearch.Instance.GetNameWithCache(auction.Bids.FirstOrDefault().Bidder)} for {auction.HighestBidAmount}";
            Task.Run(() => Send(sub.UserId, "New bid", text, AuctionUrl(auction), ItemIconUrl(auction.Tag), auction));
        }

        internal void AuctionOver(SubscribeItem sub, SaveAuction auction)
        {
            var text = $"Highest bid is {auction.HighestBidAmount}";
            Task.Run(() => Send(sub.UserId, $"Auction for {auction.ItemName} ended", text, AuctionUrl(auction), ItemIconUrl(auction.Tag), FormatAuction(auction)));
        }

        internal void PriceAlert(SubscribeItem sub, string productId, double value)
        {
            var text = $"{ItemDetails.TagToName(productId)} reached {value.ToString("0.00")}";
            Task.Run(() => Send(sub.UserId, $"Price Alert", text, $"{BaseUrl}/item/{productId}", ItemIconUrl(productId)));
        }

        internal void AuctionPriceAlert(SubscribeItem sub, SaveAuction auction)
        {
            var text = $"New Auction for {auction.ItemName} for {auction.StartingBid}";
            Task.Run(() => Send(sub.UserId, $"Price Alert", text, AuctionUrl(auction), ItemIconUrl(auction.Tag), FormatAuction(auction)));
        }

        private object FormatAuction(SaveAuction auction)
        {
            return new { type = "auction", auction = JsonConvert.SerializeObject(auction) };
        }

        string AuctionUrl(SaveAuction auction)
        {
            return BaseUrl + "/auction/" + auction.Uuid;
        }

        string ItemIconUrl(string tag)
        {
            return ItemIconsBase + $"/{tag}";
        }
    }
}