using System.Threading.Tasks;

namespace hypixel
{
    public abstract class Command
    {
        protected const int A_MINUTE = 60;
        protected const int TEN_MINUTES = A_MINUTE*10;
        protected const int A_HOUR = A_MINUTE*60;
        protected const int A_DAY = A_HOUR*24;
        protected const int A_WEEK = A_DAY * 7;
        public abstract Task Execute(MessageData data);
    }
}
