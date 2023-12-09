namespace Coflnet.Sky.Core
{
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum Tier {
        UNKNOWN,
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY,
        SPECIAL,
        VERY_SPECIAL,
        MYTHIC,
        SUPREME,
        DIVINE = 9,
        ULTIMATE
    }
}