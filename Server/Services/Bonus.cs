using System;

namespace Coflnet.Sky.Core
{
    public class Bonus
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public BonusType Type { get; set; }
        public TimeSpan BonusTime { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public string ReferenceData { get; set; }

        public enum BonusType
        {
            REFERAL,
            BEING_REFERED,
            FEEDBACK,
            /// <summary>
            /// A refered user upgraded to a premium plan
            /// </summary>
            REFERED_UPGRADE,
            PURCHASE
        }
    }
}