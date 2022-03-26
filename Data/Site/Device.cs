using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class Device
    {
        [IgnoreMember]
        public int Id {get;set;}
        [IgnoreMember]
        public int UserId {get;set;}
        [Key("conId")]
        [System.ComponentModel.DataAnnotations.MaxLength(32)]
        public string ConnectionId {get;set;}
        [Key("name")]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        public string Name {get;set;}
        [Key("token")]
        public string Token {get;set;}
    }
}
