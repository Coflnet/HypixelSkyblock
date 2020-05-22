using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace hypixel
{
    public class UuId 
    {
        public int Id {get;set;}
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string value {get;set;}

        public UuId(string value)
        {
            this.value = value;
        }

        public static implicit operator string(UuId id) => id.value;
        public static implicit operator UuId(string id) => new UuId(id);
    }


    public class Player
    {
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string UuId {get;set;}
        [MaxLength(16)]
        public string Name {get;set;}
        
        [ForeignKey("AuctioneerId")]
        public List<SaveAuction> Auctions {get;set;}

        [ForeignKey("Bidder")]
        public List<SaveBids> Bids {get;set;}
        
        [Timestamp]
        public System.DateTime UpdatedAt {get;set;}
        
        public bool ChangedFlag {get;set;}

        public Player() { }

        public Player(string uuid)
        {
            this.UuId = uuid;
        }



        public override bool Equals(object obj)
        {
            return obj is Player player &&
                   UuId == player.UuId;
        }

        public override int GetHashCode()
        {
            int hashCode = -792624511;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(UuId);
            return hashCode;
        }
    }
}