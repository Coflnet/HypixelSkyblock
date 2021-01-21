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
    public class NotificationService
    {
        public static NotificationService Instance { get; set; }

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
                    if (!hasPremium && user.Devices.Count >= 1)
                        throw new CoflnetException("no_premium", "You need premium to add multiple devices");
                    var device = new Device() { UserId = user.Id, Name = deviceName, Token = token };
                    user.Devices.Add(device);
                    context.Update(user);
                    context.Add(device);
                }
                context.SaveChanges();

            }
        }

        internal async Task Send(int userId, string text, string url)
        {
            Console.WriteLine("Sending: " + text);
            using (var context = new HypixelContext())
            {
                var devices = context.Users.Where(u => u.Id == userId).SelectMany(u => u.Devices);
                foreach (var item in devices)
                {
                    Console.WriteLine("sending " + item.UserId);
                    var success = await NotifyAsync(item.Token, "Skyblock Notification", text,url);
                    context.Remove(item);
                }
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> NotifyAsync(string to, string title, string body,string click_action = null)
        {
            try
            {
                // Get the server key from FCM console
                var serverKey = string.Format("key={0}", SimplerConfig.Config.Instance["firebaseKey"]);

                // Get the sender id from FCM console
                var senderId = string.Format("id={0}", SimplerConfig.Config.Instance["firebaseSenderId"]);
                
                var icon = "https://sky.coflnet.com/logo192.png";

                var data = new
                {
                    to, // Recipient device token
                    notification = new { title, body,click_action,icon }
                };

                // Using Newtonsoft.Json
                var jsonBody = JsonConvert.SerializeObject(data);
                Console.WriteLine("created body");

                var client = new RestClient();
                var request = new RestRequest("https://fcm.googleapis.com/fcm/send", Method.POST);
                request.AddHeader("Authorization", serverKey);
                request.AddHeader("Sender", senderId);
                request.AddJsonBody(data);

                var response = await client.ExecuteAsync(request);
                Console.WriteLine("sent with response:");
                Console.WriteLine(response.Content);

                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown in Notify Service: {ex}");
            }
            Console.WriteLine("done");

            return false;
        }
    }
}