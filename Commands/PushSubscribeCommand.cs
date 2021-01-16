using System;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class UserNotFoundException : CoflnetException
    {
        public UserNotFoundException(string id) : base("user_not_found", $"There is no user with the id {id}")
        {
        }
    }

    public class NoPremiumException : CoflnetException
    {
        public NoPremiumException(string message) : base("no_premium", message)
        {
        }
    }


    public class PushSubscribeCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();

            using (var context = new HypixelContext())
            {
                var user = data.User;

                var subscriptions = context.SubscribeItem.Where(s => s.UserId == user.Id);
                if (!user.HasPremium && subscriptions.Count() >= 3)
                    throw new NoPremiumException("Nonpremium users can only have 3 subscriptions");


                SubscribeEngine.Instance.AddNew(new SubscribeItem()
                {
                    GeneratedAt = DateTime.Now,
                    Price = args.Price,
                    TopicId = args.Topic,
                    Type = args.Type,
                    UserId = user.Id
                });
            }
            data.Ok();
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("price")]
            public long Price;
            [Key("topic")]
            public string Topic;
            [Key("type")]
            public SubscribeItem.SubType Type;
        }
    }
}