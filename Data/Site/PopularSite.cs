using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Coflnet.Sky.Core
{
    [DataContract]
    public class PopularSite
    {
        [DataMember(Name = "title")]
        public string Title;
        [DataMember(Name = "url")]
        public string Url;

        public PopularSite(string title, string url)
        {
            Title = title;
            Url = url;
        }

        public override bool Equals(object obj)
        {
            return obj is PopularSite site &&
                   Title == site.Title &&
                   Url == site.Url;
        }

        public override int GetHashCode()
        {
            int hashCode = -1359334193;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Title);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Url);
            return hashCode;
        }
    }
}