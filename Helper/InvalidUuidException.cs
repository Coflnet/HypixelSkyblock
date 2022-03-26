namespace Coflnet.Sky.Core
{
    public class InvalidUuidException : CoflnetException
    {
        public InvalidUuidException(string uuid) : base("invalid_uuid", $"The uuid {uuid} is invalid")
        {
        }
    }
}
