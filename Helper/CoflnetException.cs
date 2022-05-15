using System;
using System.Collections;
using System.Runtime.Serialization;

namespace Coflnet.Sky.Core
{
    public class CoflnetException : Exception
    {
        public string Slug;
        public string Trace;

        public CoflnetException(string slug, string message) : base(message)
        {
            Slug = slug;
        }

        public override bool Equals(object obj)
        {
            return obj is CoflnetException ex
            && ex.Trace == Trace
            && ex.Message == Message
            && ex.Slug == Slug;
        }

        public override Exception GetBaseException()
        {
            return base.GetBaseException();
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Slug, Message, Trace);
        }

        public override string ToString()
        {
            return $"{Message} ({Trace}, {Slug})\n{StackTrace}";
        }
    }
}
