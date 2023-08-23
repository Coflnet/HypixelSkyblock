using System.Collections;
using System.Collections.Generic;
using OpenTracing.Propagation;

namespace Coflnet.Tracing
{
    public class TextMap : Dictionary<string, string>, ITextMap
    {
        public void Set(string key, string value)
        {
            this[key] = value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
