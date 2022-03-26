using System;

namespace Coflnet.Sky.Core
{
    public class CoflnetException : Exception
    {
        public string Slug;

        
        public CoflnetException(string slug, string message) : base(message)
        {
            Slug = slug;
        }
    }
}
