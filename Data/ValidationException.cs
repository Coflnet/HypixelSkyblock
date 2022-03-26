namespace Coflnet.Sky.Core
{
    public class ValidationException : CoflnetException
    {
        public ValidationException(string message) : base("validation_error",message)
        {

        }
    }
}
