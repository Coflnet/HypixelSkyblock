using System;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class SubscribeItem
    {
        public int Id{get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        public string ItemTag {get;set;}
        
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string PlayerUuid {get;set;}

        public long Price {get;set;}

        [System.ComponentModel.DataAnnotations.Timestamp]
        public DateTime GeneratedAt {get;set;}

        public enum SubType
        {
            PRICE_LOWER_THAN = 1,
            PRICE_HIGHER_THAN = 2,
            OUTBID = 4
        }

        public SubType Type  {get;set;}

        public string Token {get;set;}

        [System.ComponentModel.DataAnnotations.MaxLength(32)]
        public string Initiator {get;set;}

    }
}
