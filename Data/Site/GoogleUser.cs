using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coflnet.Sky.Core
{
    public class GoogleUser
    {
        public static bool EveryoneIsPremium { get;  private set; }
        static GoogleUser()
        {
            bool.TryParse(SimplerConfig.SConfig.Instance["EveryonePremium"], out bool allprem);
            EveryoneIsPremium = allprem;
        }

        public int Id { get; set; }
        public DateTime PremiumExpires { get; set; }
        public DateTime CreatedAt { get; set; }
        [Column(TypeName = "char(32)")]
        public string GoogleId { get; set; }
        public string Email { get; set; }
        [Column(TypeName = "char(32)")]
        public string MinecraftUuid { get; set; }
        public int ReferedBy { get; set; }
        public List<Device> Devices { get; set; }

        [NotMapped]
        public bool HasPremium => PremiumExpires > DateTime.Now || EveryoneIsPremium;

        public GoogleUser()
        {
            CreatedAt = DateTime.Now;
        }
    }
}