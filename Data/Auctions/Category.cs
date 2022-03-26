namespace Coflnet.Sky.Core
{
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum Category {
        UNKNOWN,
        WEAPON,
        ARMOR,
        ACCESSORIES,
        CONSUMABLES,
        BLOCKS,
        MISC

    }
}