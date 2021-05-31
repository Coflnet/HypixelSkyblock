/*using StackExchange.Redis;

namespace hypixel
{
    public class RedisItemPrices : ItemPrices
    {
        public static ConnectionMultiplexer redis;

        static RedisItemPrices()
        {
            ConfigurationOptions options = ConfigurationOptions.Parse(SimplerConfig.Config.Instance["redisCon"]);
            options.Password = SimplerConfig.Config.Instance["redisPassword"];
            redis = ConnectionMultiplexer.Connect(options);

            var db = redis.GetDatabase();
            db.StringSet("a", "xy", System.TimeSpan.FromDays(1));
            var v = db.StringGetAsync("a");
            System.Console.WriteLine(v);


        }



    }
}
*/