namespace Coflnet.Sky.Core
{
    public class UserNotFoundException : CoflnetException
    {
        public UserNotFoundException(string id) : base("user_not_found", $"There is no user with the id {id}")
        {
        }
    }
}