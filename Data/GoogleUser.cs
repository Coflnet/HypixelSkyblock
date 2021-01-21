using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace hypixel
{
    public class GoogleUser
    {
        public int Id {get;set;}
        public DateTime PremiumExpires {get;set;}
        public DateTime CreatedAt {get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(32)]
        public string GoogleId {get;set;}
        public string Email {get;set;}
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string MinecraftUuid {get;set;}
        public List<Device> Devices {get;set;}

        [NotMapped]
        public bool HasPremium => PremiumExpires > DateTime.Now; 

        public GoogleUser()
        {
            CreatedAt = DateTime.Now;
        }
    }
}