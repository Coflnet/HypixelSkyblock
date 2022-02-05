using Newtonsoft.Json;

namespace hypixel
{
    public class JSON
    {
        public static string Stringify<T>(T value)
        {
            return JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            });
        }
    }
}
