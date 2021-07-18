using System;
using System.IO;
using System.Text;
using System.Threading;
using Coflnet;
using RestSharp;
using WebSocketSharp.Server;
using WebSocketSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using WebSocketSharp.Net;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using RateLimiter;
using Microsoft.EntityFrameworkCore;
using MessagePack;
using System.Collections.Generic;
using Prometheus;
using System.Diagnostics;

namespace hypixel
{
    public class Server
    {

        public Server()
        {
            Limiter = new IpRateLimiter(ip =>
            {
                var constraint = new CountByIntervalAwaitableConstraint(10, TimeSpan.FromSeconds(1));
                var constraint2 = new CountByIntervalAwaitableConstraint(35, TimeSpan.FromSeconds(10));

                // Compose the two constraints
                return TimeLimiter.Compose(constraint, constraint2);
            });
        }
        HttpServer server;

        ConcurrentDictionary<string, int> ConnectionToUserId = new ConcurrentDictionary<string, int>();
        private IpRateLimiter Limiter;

        /// <summary>
        /// Starts the backend server
        /// </summary>
        public async Task Start(short port = 8008, string urlPath = "/skyblock")
        {
            server = new HttpServer(port);

            server.AddWebSocketService<SkyblockBackEnd>(urlPath);
            // do NOT timeout after 60 sec
            server.KeepClean = false;
            server.OnOptions += (sender, e) =>
            {
                e.Response.AppendHeader("Allow", "OPTIONS, GET, POST");
                e.Response.AppendHeader("access-control-allow-origin", "*");
                e.Response.AppendHeader("Access-Control-Allow-Headers", "*");
                return Task.CompletedTask;
            };
            var getRequests = Metrics
                    .CreateCounter("total_get_requests", "Number of processed http GET requests");
            server.OnGet += async (sender, e) =>
            {
                getRequests.Inc();
                var getEvent = e as HttpRequestEventArgs;
                e.Response.AppendHeader("Allow", "OPTIONS, GET");
                e.Response.AppendHeader("access-control-allow-origin", "*");
                e.Response.AppendHeader("Access-Control-Allow-Headers", "*");
                try
                {
                    try
                    {
                        await AnswerGetRequest(new WebsocketRequestContext(e));
                    }
                    catch (CoflnetException ex)
                    {
                        getEvent.Response.StatusCode = 500;
                        getEvent.Response.SendChunked = true;
                        getEvent.Response.WriteContent(Encoding.UTF8.GetBytes(ex.Message));
                        return;
                    }

                }
                catch (Exception ex)
                {
                    dev.Logger.Instance.Error(ex, $"Ran into an error on get `{e.Request.RawUrl}`");
                    return;
                }

            };

            server.OnPost += async (sender, e) =>
            {

                if (e.Request.RawUrl == "/stripe")
                    await new StripeRequests().ProcessStripe(e);
                //if (e.Request.RawUrl.StartsWith("/command/"))
                //    await HandleCommand(e.Request, e.Response);

            };

            server.Start();
            Console.WriteLine("started http");
            //Console.ReadKey (true);
            await Task.Delay(Timeout.Infinite);
            server.Stop();
        }

        public abstract class RequestContext
        {
            public abstract string path { get; }
            public abstract string HostName { get; }
            public abstract Task WriteAsync(string data);
            public abstract void WriteAsync(byte[] data);
            public abstract void SetContentType(string type);
            public abstract void SetStatusCode(int code);
            public abstract void AddHeader(string name, string value);
            public abstract void Redirect(string uri);
            public abstract IDictionary<string, string> QueryString { get; }
            public abstract string UserAgent { get; }

            internal virtual void ForceSend()
            {
                // do nothing
            }
        }

        public class WebsocketRequestContext : RequestContext
        {
            public HttpRequestEventArgs original;

            public WebsocketRequestContext(HttpRequestEventArgs original)
            {
                this.original = original;
                this.original.Response.SendChunked = true;
            }

            public override string HostName => original.Request.UserHostName;

            public override IDictionary<string, string> QueryString => (IDictionary<string, string>)original.Request.QueryString;

            public override string path => original.Request.RawUrl;

            public override string UserAgent => original.Request?.UserAgent;

            public override void AddHeader(string name, string value)
            {
                original.Response.AppendHeader(name, value);
            }

            public override void Redirect(string uri)
            {
                original.Response.Redirect(uri);

            }

            public override void SetContentType(string type)
            {
                original.Response.ContentType = type;
            }

            public override void SetStatusCode(int code)
            {
                original.Response.StatusCode = code;
            }

            public override Task WriteAsync(string data)
            {
                return original.Response.WritePartial(data);
            }

            public override void WriteAsync(byte[] data)
            {
                original.Response.WriteContent(data);
            }

            internal override void ForceSend()
            {
                original.Response.OutputStream.Flush();
            }
        }

        public async Task AnswerGetRequest(RequestContext context)
        {
            var path = context.path.Split('?')[0];


            if (path == "/stats" || path.EndsWith("/status") || path.Contains("show-status"))
            {
                PrintStatus(context);
                Console.WriteLine(DateTime.Now);
                return;
            }

            if (path.StartsWith("/command/"))
            {
                await HandleCommand(context);
                return;
            }

            if (path == "/low")
            {
                var relevant = Updater.LastAuctionCount.Where(a => a.Value > 0 && a.Value < 72);
                await context.WriteAsync(JSON.Stringify(relevant));
                return;
            }

            if (context.HostName.StartsWith("skyblock") && !Program.LightClient)
            {
                context.Redirect("https://sky.coflnet.com" + path);
                return;
            }

            if (path == "/" || path.IsNullOrEmpty())
            {
                path = "index.html";
            }

            if (path == "/players")
            {
                await PrintPlayers(context);
                return;
            }
            if (path == "/items")
            {
                await PrintItems(context);
                return;
            }

            if (path == "/api/items/bazaar")
            {
                await PrintBazaarItems(context);
                return;
            }
            if (path == "/api/items/search")
            {
                await SearchItems(context);
                return;
            }



            byte[] contents;
            var relativePath = $"files/{path}";

            if (path.StartsWith("/static/skin"))
            {
                GetSkin(relativePath);
            }


            if (!FileController.Exists(relativePath))
            {
                //res.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                //return;
                // vue.js will handle it internaly
                relativePath = $"files/index.html";
            }


            try
            {
                contents = FileController.ReadAllBytes(relativePath);
            }
            catch (Exception)
            {
                await context.WriteAsync("File not found, maybe you fogot to upload the fronted");
                return;
            }

            context.SetContentType("text/html");
            //context.ContentEncoding = Encoding.UTF8;

            if (relativePath == "files/index.html" && !path.EndsWith(".js") && !path.EndsWith(".css"))
            {
                var watch = Stopwatch.StartNew();
                await HtmlModifier.ModifyContent(path, contents, context);

                if (context is WebsocketRequestContext httpContext)
                    if (path.StartsWith("/static"))
                        TrackingService.Instance.TrackPage(httpContext.original.Request?.Url?.ToString(),
                                                    "",
                                                    null,
                                                    null,
                                                    watch.Elapsed);
                    else
                        TrackingService.Instance.TrackPage(httpContext.original.Request?.Url?.ToString(),
                                "",
                                httpContext.original.Request?.UrlReferrer?.ToString(),
                                httpContext.original.Request?.UserAgent,
                                watch.Elapsed);
                return;
            }



            if (path.EndsWith(".png") || path.StartsWith("/static/skin"))
            {
                context.SetContentType("image/png");
            }
            else if (path.EndsWith(".css"))
            {
                context.SetContentType("text/css");
            }
            if (path.EndsWith(".js") || path.StartsWith("/static/js"))
            {
                context.SetContentType("text/javascript");
            }
            if (relativePath == "files/index.html")
            {
                context.AddHeader("cache-control", "private");
                context.SetStatusCode(404);
                await context.WriteAsync("/* This file was not found. Retry in a few miniutes :) */");
                return;
            }

            context.AddHeader("cache-control", "public,max-age=" + (3600 * 24 * 30));

            context.WriteAsync(contents);
        }




        private async Task HandleCommand(RequestContext context)
        {
            HttpMessageData data = new HttpMessageData(context);
            try
            {
                /*var conId = req.Headers["ConId"];
                if (conId == null || conId.Length < 32)
                    throw new CoflnetException("invalid_conid", "The 'ConId' Header has to be at least 32 characters long and generated randomly");
                conId = conId.Truncate(32);
                data.SetUserId = id =>
                {
                    this.ConnectionToUserId.TryAdd(conId, id);
                };

                if (ConnectionToUserId.TryGetValue(conId, out int userId))
                    data.UserId = userId;

                if (data.Type == "test")
                {
                    Console.WriteLine(req.RemoteEndPoint.Address.ToString());
                    foreach (var item in req.Headers.AllKeys)
                    {
                        Console.WriteLine($"{item.ToString()}: {req.Headers[item]}");
                    }
                    return;
                } */

                if (await CacheService.Instance.TryFromCacheAsync(data))
                    return;

                /*  var ip = req.Headers["Cf-Connecting-Ip"];
                  if(ip == null)
                      ip = req.Headers["X-Real-Ip"];
                  if(ip == null)
                      ip = req.RemoteEndPoint.Address.ToString();
                  Console.WriteLine($"rc {data.Type} {data.Data.Truncate(20)}");
                  await Limiter.WaitUntilAllowed(ip); */
                Console.Write($"r {data.Type} {data.Data.Truncate(15)} ");
                //ExecuteCommandWithCache
                if (SkyblockBackEnd.Commands.TryGetValue(data.Type, out Command command))
                {
                    try
                    {
                        ExecuteCommand(data, command);
                        return;
                    }
                    catch (CoflnetException ex)
                    {
                        context.SetStatusCode(400);
                        await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException is CoflnetException ex)
                        {
                            context.SetStatusCode(400);
                            await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));

                        }
                        else
                        {
                            Console.WriteLine("holly shit");
                            data.CompletionSource.TrySetException(e);
                            dev.Logger.Instance.Error(e);
                            throw e;
                        }
                    }
                }
                else
                    throw new CoflnetException("unkown_command", "Command not known, check the docs");
            }
            catch (CoflnetException ex)
            {
                context.SetStatusCode(400);
                await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
            }
            catch (Exception ex)
            {
                context.SetStatusCode(500);
                await data.SendBack(new MessageData("error", JsonConvert.SerializeObject(new { Slug = "error", Message = "An unexpected internal error occured, make sure the format of Data is correct" })));
                TrackingService.Instance.CommandError(data.Type);
                dev.Logger.Instance.Error($"Fatal error on Command {JsonConvert.SerializeObject(data)} {ex.Message} {ex.StackTrace}\n {ex.InnerException?.Message} {ex.InnerException?.StackTrace}");
            }
        }

        public static void ExecuteCommandHeadless(MessageData data)
        {
            if (!SkyblockBackEnd.Commands.TryGetValue(data.Type, out Command command))
                return; // unkown command

            try
            {
                command.Execute(data);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Failed to update cache");
            }
        }

        private static void ExecuteCommand(HttpMessageData data, Command command)
        {
            command.Execute(data);

            if (!data.CompletionSource.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new CoflnetException("timeout", "could not generate a response, please report this and try again");
            }
        }

        public static async Task<TRes> ExecuteCommandWithCache<TReq, TRes>(string command, TReq reqdata)
        {
            var source = new TaskCompletionSource<TRes>();
            var data = new ProxyMessageData<TReq, TRes>(command, reqdata, source);
            if (!CacheService.Instance.TryFromCache(data))
                await SkyblockBackEnd.Commands[command].Execute(data);
            return source.Task.Result;
        }

        public class ProxyMessageData<Treq, TRes> : MessageData
        {
            private readonly Treq request;
            TaskCompletionSource<TRes> source;

            public ProxyMessageData(string type, Treq request, TaskCompletionSource<TRes> source) : base(type, "", 0)
            {
                this.request = request;
                this.Type = type;
                this.source = source;
                this.Data = MessagePack.MessagePackSerializer.ToJson<Treq>(request);
            }

            public override T GetAs<T>()
            {
                return (T)(object)request;
            }

            public override MessageData Create<T>(string type, T a, int maxAge = 0)
            {
                source.SetResult((TRes)(object)a);
                var d = base.Create<T>(type, a, maxAge);
                d.Data = MessagePackSerializer.ToJson(a);
                return d;
            }


            public override Task SendBack(MessageData data, bool cache = true)
            {
                try
                {
                    if (source.TrySetResult(MessagePackSerializer.Deserialize<TRes>(MessagePackSerializer.FromJson(data.Data))))
                    { /* nothing to do, already set */ }
                }
                catch (Exception)
                {
                    // thrown excpetion, looks like it isn't messagepack
                    if (source.TrySetResult(JsonConvert.DeserializeObject<TRes>(data.Data)))
                    { /* nothing to do, already set */ }
                }

                if (cache)
                    CacheService.Instance.Save(this, data, 0);
                return Task.CompletedTask;
            }
        }



        private static void GetSkin(string relativePath)
        {
            if (!FileController.Exists(relativePath))
            {
                // try to get it from mojang
                var client = new RestClient("https://textures.minecraft.net/");
                var request = new RestRequest("/texture/{id}");
                request.AddUrlSegment("id", Path.GetFileName(relativePath));
                Console.WriteLine(Path.GetFileName(relativePath));
                var fullPath = FileController.GetAbsolutePath(relativePath);
                FileController.CreatePath(fullPath);
                var inStream = new MemoryStream(client.DownloadData(request));

                //client.DownloadData(request).SaveAs(fullPath+ "f.png" );

                // parse it to only show face
                // using (var inStream = new FileStream(File.Open("fullPath",FileMode.Rea)))
                using (var outputImage = new Image<Rgba32>(16, 16))
                {
                    var baseImage = SixLabors.ImageSharp.Image.Load(inStream);

                    var lowerImage = baseImage.Clone(
                                    i => i.Resize(256, 256)
                                        .Crop(new Rectangle(32, 32, 32, 32)));

                    lowerImage.Save(fullPath + ".png");

                }
                FileController.Move(relativePath + ".png", relativePath);
            }
        }



        public static void AddPremiumTime(int days, GoogleUser user)
        {
            if (user.PremiumExpires > DateTime.Now)
                user.PremiumExpires += TimeSpan.FromDays(days);
            else
                user.PremiumExpires = DateTime.Now + TimeSpan.FromDays(days);
        }

        private static void PrintStatus(RequestContext res)
        {
            var data = new Stats()
            {
                NameRequests = Program.RequestsSinceStart,
                Indexed = Indexer.IndexedAmount,
                LastIndexFinish = Indexer.LastFinish,
                LastBazaarUpdate = dev.BazaarUpdater.LastUpdate,
                LastNameUpdate = NameUpdater.LastUpdate,
                CacheSize = CacheService.Instance.CacheSize,
                QueueSize = Indexer.QueueCount,
                FlipSize = Flipper.FlipperEngine.Instance.QueueSize,
                LastAuctionPull = Updater.LastPull,
                LastUpdateSize = Updater.UpdateSize,
                SubscriptionTobics = SubscribeEngine.Instance.SubCount,
                ConnectionCount = SkyblockBackEnd.ConnectionCount
            };

            // determine status
            res.SetStatusCode(200);
            var maxTime = DateTime.Now.Subtract(new TimeSpan(0, 5, 0));
            if (!Program.LightClient && (data.LastIndexFinish < maxTime
                || data.LastBazaarUpdate < maxTime
                || data.LastNameUpdate < maxTime
                || data.LastAuctionPull < maxTime))
            {
                res.SetStatusCode(500);
            }


            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            res.WriteAsync(json).GetAwaiter().GetResult();
        }

        private static async Task PrintBazaarItems(RequestContext context)
        {
            var data = await ItemDetails.Instance.GetBazaarItems();


            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data.Select(i => new { i.Name, i.Tag, i.MinecraftType, i.IconUrl }));
            await context.WriteAsync(json);
        }

        private static async Task PrintPlayers(RequestContext reqcon)
        {
            using (var context = new HypixelContext())
            {
                var data = context.Players.OrderByDescending(p => p.UpdatedAt).Select(p => new { p.Name, p.UuId }).Take(10000).AsParallel();
                StringBuilder builder = GetSiteBuilder("Player");
                foreach (var item in data)
                {
                    if (item.Name == null)
                        continue;
                    builder.AppendFormat("<li><a href=\"{0}\">{1}</a></li>", $"/player/{item.UuId}/{item.Name}", $"{item.Name} auctions");
                }
                await reqcon.WriteAsync(builder.ToString());
            }
        }

        private static async Task PrintItems(RequestContext reqcon)
        {
            using (var context = new HypixelContext())
            {
                var data = await context.Items.Select(p => new { p.Tag }).Take(10000).ToListAsync();
                StringBuilder builder = GetSiteBuilder("Item");
                foreach (var item in data)
                {
                    var name = ItemDetails.TagToName(item.Tag);
                    builder.AppendFormat("<li><a href=\"{0}\">{1}</a></li>", $"/item/{item.Tag}/{name}", $"{name} auctions");
                }
                await reqcon.WriteAsync(builder.ToString());
            }
        }

        private static StringBuilder GetSiteBuilder(string topic)
        {
            var builder = new StringBuilder(20000);
            builder.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
            builder.Append($"<link rel=\"icon\" href=\"/favicon.ico\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/><title>{topic} List</title>");
            builder.Append("<style>li {padding:10px;}</style></head><body>");
            builder.Append($"<h2>List of the most recently updated {topic}s</h2><a href=\"https://sky.coflnet.com\">back to the start page</a><ul>");
            return builder;
        }

        private static async Task SearchItems(RequestContext context)
        {
            var term = context.QueryString["term"];
            Console.WriteLine("searchig for:");
            Console.WriteLine(term);
            var data = await ItemDetails.Instance.Search(term);


            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            await context.WriteAsync(json);
        }

        public void Stop()
        {
            server.Stop();
        }
    }

    public static class ResponseExtentions
    {
        public static async Task WritePartial(
            this HttpListenerResponse response, string stringContent
        )
        {
            if (response == null)
                throw new ArgumentNullException("response");

            if (stringContent == null)
                throw new ArgumentNullException("content");

            var content = Encoding.UTF8.GetBytes(stringContent);

            var len = content.LongLength;
            if (len == 0)
            {
                return;
            }

            var output = response.OutputStream;

            if (len <= Int32.MaxValue)
                await output.WriteAsync(content, 0, (int)len);
            else
                output.WriteBytes(content, 1024);
        }
        public static async Task WriteEnd(
            this Server.RequestContext response, string stringContent
        )
        {
            await response.WriteAsync(stringContent + "</body></html>");
            //response.Close();

        }

        internal static void WriteBytes(
            this Stream stream, byte[] bytes, int bufferLength
            )
        {
            using (var src = new MemoryStream(bytes))
                src.CopyTo(stream, bufferLength);
        }


        public static string RedirectSkyblock(this Server.RequestContext res, string parameter = null, string type = null, string seoTerm = null)
        {
            var url = $"https://sky.coflnet.com" + (type == null ? "" : $"/{type}") + (parameter == null ? "" : $"/{parameter}") + (seoTerm == null ? "" : $"/{seoTerm}");
            res.Redirect(url);
            //res.Close();
            return url;
        }
    }
}
