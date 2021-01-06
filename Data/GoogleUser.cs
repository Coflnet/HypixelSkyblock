using System;

namespace hypixel
{
    public class GoogleUser
    {
        public int Id {get;set;}
        public DateTime PremiumExpires {get;set;}
        public DateTime CreatedAt {get;set;}
        public string GoogleId {get;set;}
        public string PaymentSessionId {get;set;}
        public string SessionId {get;set;}
        public string Email {get;set;}
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string MinecraftUuid {get;set;}

        public GoogleUser()
        {
            CreatedAt = DateTime.Now;
        }
    }
}