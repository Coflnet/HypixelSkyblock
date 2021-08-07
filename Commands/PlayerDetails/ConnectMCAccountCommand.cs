using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace hypixel
{
    public class ConnectMCAccountCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            var userId = data.UserId;
            var time = DateTime.Now;
            int amount = GetAmount(userId, time);

            var player = await PlayerService.Instance.GetPlayer(uuid);
            if (player == default(Player))
                throw new CoflnetException("unkown_player", "This player was not found");

            var sub = new VerifySub(a =>
            {
                int amount = GetAmount(userId, time);
                int lastAmount = GetAmount(userId, DateTime.Now.Subtract(TimeSpan.FromMinutes(5)));
                var code = a.StartingBid;
                if (a.AuctioneerId != uuid)
                    code = a.Bids.Where(u => u.Bidder == uuid).Select(b => b.Amount).Where(b => b % 1000 == amount || b % 1000 == lastAmount).FirstOrDefault();
                Console.WriteLine("vertifying " + code);
                if (code % 1000 == amount || code % 1000 == lastAmount)
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

        private static int GetAmount(int userId, DateTime time)
        {
            var tokenString = LoginExternalCommand.GenerateToken(userId + time.RoundDown(TimeSpan.FromMinutes(10)).ToString());
            var amount = BitConverter.ToInt32(Encoding.ASCII.GetBytes(tokenString.Truncate(4))) % 980 + 19;
            return amount;
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