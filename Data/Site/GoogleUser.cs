using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [MaxLength(64)]
        public string TermsAcceptedVersion { get; set; }
        [ConcurrencyCheck]
        public DateTime? TermsAcceptedAtUtc { get; set; }
        [Column(TypeName = "char(64)")]
        public string TermsAcceptedHash { get; set; }
        [MaxLength(32)]
        public string TermsAcceptanceSource { get; set; }

        [NotMapped]
        public bool HasPremium => PremiumExpires > DateTime.Now || EveryoneIsPremium;

        public GoogleUser()
        {
            CreatedAt = DateTime.Now;
        }

        public bool ApplyTermsAcceptance(TermsAcceptance acceptance)
        {
            acceptance = acceptance.Validate();
            if (TermsAcceptedVersion == acceptance.Version
                && string.Equals(TermsAcceptedHash, acceptance.Hash, StringComparison.OrdinalIgnoreCase))
                return false;
            if (TermsAcceptedAtUtc >= acceptance.AcceptedAtUtc)
                throw new InvalidOperationException("A newer terms acceptance is already recorded");

            TermsAcceptedVersion = acceptance.Version;
            TermsAcceptedHash = acceptance.Hash;
            TermsAcceptedAtUtc = acceptance.AcceptedAtUtc;
            TermsAcceptanceSource = acceptance.Source;
            return true;
        }
    }
}
