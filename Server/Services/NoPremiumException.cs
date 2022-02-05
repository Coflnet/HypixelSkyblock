namespace hypixel
{
    public class NoPremiumException : CoflnetException
    {
        public NoPremiumException(string message) : base("no_premium", message)
        {
        }
    }
}