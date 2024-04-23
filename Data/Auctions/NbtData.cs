using fNbt;
using fNbt.Tags;
using MessagePack;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class NbtData
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [IgnoreMember]
        [JsonIgnore]
        public int Id { get; set; }

        [Key(0)]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        [System.Text.Json.Serialization.JsonIgnore]
        public byte[] data { get; set; }

        public void SetData(string data)
        {
            this.data = NBT.Extra(data);
        }

        public void SetData(NbtCompound data)
        {
            this.data = NBT.Extra(data);
        }

        public NbtFile Content()
        {
            return NBT.File(data, NbtCompression.None);
        }

        public NbtCompound Root()
        {
            return Content().RootTag;
        }

        [IgnoreMember]
        [NotMapped]
        public Dictionary<string, object> Data
        {
            get
            {
                return AsDictonary(Root());
            }
        }

        public static Dictionary<string, object> AsDictonary(NbtCompound top)
        {
            if(top == null)
                return null;
            var dict = new Dictionary<string, object>();
            foreach (var item in top)
            {
                switch (item.TagType)
                {
                    case NbtTagType.Byte:
                        dict.Add(item.Name, item.ByteValue);
                        break;
                    case NbtTagType.ByteArray:
                        dict.Add(item.Name, item.ByteArrayValue);
                        break;
                    case NbtTagType.Compound:
                        dict.Add(item.Name, AsDictonary(top.Get<NbtCompound>(item.Name)));
                        break;
                    case NbtTagType.Double:
                        dict.Add(item.Name, item.DoubleValue);
                        break;
                    case NbtTagType.Float:
                        dict.Add(item.Name, item.FloatValue);
                        break;
                    case NbtTagType.Int:
                        dict.Add(item.Name, item.IntValue);
                        break;
                    case NbtTagType.IntArray:
                        dict.Add(item.Name, item.IntArrayValue);
                        break;
                    case NbtTagType.Long:
                        dict.Add(item.Name, item.LongValue);
                        break;
                    case NbtTagType.Short:
                        dict.Add(item.Name, item.ShortValue);
                        break;
                    case NbtTagType.String:
                        dict.Add(item.Name, item.StringValue);
                        break;
                    case NbtTagType.List:
                        dict.Add(item.Name,top.Get<NbtList>(item.Name).Select<NbtTag,object>(i=> {
                            var child = i as NbtCompound;
                            if(child != null)
                                return AsDictonary(child);
                            if(i is NbtString s)
                                return s.StringValue;
                            // return default string representation
                            return i.ToString();
                        }).OrderBy(i=>i.ToString()).ToList());
                        break;
                    default:
                        dict.Add(item.Name, item.ToString());
                        break;
                }
            }
            return dict;// top.ToDictionary(e=>e.Name,e=>(int)e.TagType == 10 ? e.ToString() : e.StringValue);
        }

        public NbtData() { }

        public NbtData(string data)
        {
            SetData(data);
        }
        public NbtData(NbtCompound data)
        {
            SetData(data);
        }
    }
}