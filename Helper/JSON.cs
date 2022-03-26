using Newtonsoft.Json;

namespace Coflnet.Sky.Core
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
