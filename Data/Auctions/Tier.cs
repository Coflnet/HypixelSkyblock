namespace hypixel
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
        DIVAN = 9

    }
}