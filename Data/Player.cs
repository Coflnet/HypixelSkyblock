using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject(true)]
    public class Player : IHitCount
    {
        [IgnoreMember]
        public int Id {get;set;}
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string UuId {get;set;}
        [MaxLength(16)]
        public string Name {get;set;}
        
        
        [Timestamp]
        public System.DateTime UpdatedAt {get;set;}
        
        public bool ChangedFlag {get;set;}

        public int HitCount {get;set;}
        public int UId {get;set;}

        public Player() { }

        public Player(string uuid)
        {
            UuId = uuid;
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