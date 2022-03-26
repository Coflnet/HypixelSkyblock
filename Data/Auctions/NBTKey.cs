namespace Coflnet.Sky.Core
{
    public class NBTKey
    {
        public short Id {get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        public string Slug {get;set;}
    }
}