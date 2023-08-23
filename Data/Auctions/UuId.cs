using System.ComponentModel.DataAnnotations.Schema;

namespace Coflnet.Sky.Core
{
    public class UuId
    {
        public int Id { get; set; }
        [Column(TypeName = "char(32)")]
        public string value { get; set; }

        public UuId(string value)
        {
            this.value = value;
        }

        public static implicit operator string(UuId id) => id.value;
        public static implicit operator UuId(string id) => new UuId(id);
    }
}