using System.Threading;

namespace Coflnet.Sky.Kafka;

// KAFKA:TLS:RELOAD_ON_CHANGE for every SkyCore Kafka client: bool.TryParse semantics, anything else keeps renewal off.
internal static class KafkaRenewalFlag
{
    private static int warned;

    internal static bool IsOn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (bool.TryParse(value, out var on))
            return on;
        if (Interlocked.Exchange(ref warned, 1) == 0)
            dev.Logger.Instance.Error("KAFKA:TLS:RELOAD_ON_CHANGE is not true/false; Kafka TLS renewal stays off");
        return false;
    }
}
