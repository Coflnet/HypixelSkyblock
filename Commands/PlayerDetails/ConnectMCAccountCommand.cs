using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace hypixel
{
    public class ConnectMCAccountCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            var amount = (new Random()).Next(20, 999);
            var userId = 1;//data.UserId;
            var player = await PlayerService.Instance.GetPlayer(uuid);
            if (player == default(Player))
                throw new CoflnetException("unkown_player", "This player was not found");

            var sub = new VerifySub(a =>
            {
                var code = a.StartingBid;
                if(a.AuctioneerId != uuid)
                    code = a.Bids.Where(u => u.Bidder == uuid).Select(b => b.Amount).Where(b => b % 1000 == amount).FirstOrDefault();
                Console.WriteLine("vertifying " + code);
                if (code % 1000 == amount)
                    using (var context = new HypixelContext())
                    {
                        var user = context.Users.Where(u => u.Id == userId).FirstOrDefault();
                        user.MinecraftUuid = a.AuctioneerId;
                        context.Update(user);
                        context.SaveChanges();
                    }
            });
            sub.Type = SubscribeItem.SubType.PLAYER;
            sub.UserId = userId;
            sub.TopicId = uuid;
            sub.Price = amount;

            SubscribeEngine.Instance.AddNew(sub);

            var response = new Response()
            {
                StartingBid = amount
            };

            await data.SendBack(data.Create("connectMc", response));
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "bid")]
            public int StartingBid;
        }

        public class VerifySub : SubscribeItem
        {
            Action<SaveAuction> OnNewAuction;

            public VerifySub(Action<SaveAuction> onNewAuction)
            {
                OnNewAuction = onNewAuction;
            }

            public override async void NotifyAuction(SaveAuction auction)
            {
                OnNewAuction(auction);
                if (this.GeneratedAt < DateTime.Now - TimeSpan.FromMinutes(10))
                    await SubscribeEngine.Instance.Unsubscribe(this.UserId, this.TopicId, this.Type);
            }
        }
    }
}