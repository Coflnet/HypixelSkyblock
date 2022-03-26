namespace Coflnet.Sky.Core
{
    public class NoPremiumException : CoflnetException
    {
        public NoPremiumException(string message) : base("no_premium", message)
        {
        }
    }
}