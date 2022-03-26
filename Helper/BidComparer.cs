using System.Collections.Generic;
using System;

namespace Coflnet.Sky.Core
{
    public class BidComparer : IEqualityComparer<SaveBids>
    {
        public bool Equals(SaveBids x, SaveBids y)
        {
            return x.Timestamp.DayOfYear == y.Timestamp.DayOfYear
            && x.Bidder == y.Bidder
            && x.Amount == y.Amount;
        }

        public int GetHashCode(SaveBids obj)
        {
            return obj.Bidder.GetHashCode() ^ obj.Timestamp.GetHashCode();
        }
    }
}