namespace hypixel
{
    public class Device
    {
        public int Id {get;set;}
        public int UserId {get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(32)]
        public string ConnectionId {get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        public string Name {get;set;}
        public string Token {get;set;}
    }
}
