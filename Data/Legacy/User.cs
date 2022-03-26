using System;
using System.Collections.Generic;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class User {
        private string _name;
        [Key (0)]
        public string uuid {get;set;}
        [Key (1)]
        public HashSet<AuctionReference> Bids = new HashSet<AuctionReference> ();
        [Key (2)]
        public Dictionary<string, SaveAuction> auctions = new Dictionary<string, SaveAuction> ();

        [Key(4)]
        public HashSet<string> auctionIds = new HashSet<string>();

        [Key(3)]
        public string Name
        {
            get
            {
               /* if((_name == null || _name == string.Empty) && Program.displayMode)
                {
                    // yep this is a racecondition
                    _name = Program.GetPlayerNameFromUuid(uuid);
                    StorageManager.Save(this);
                }*/
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is User user &&
                   uuid == user.uuid;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(uuid);
        }

        public override string ToString()
        {
            return $"User {Name} ({uuid}) ";
        }

        public User(string uuid)
        {
            this.uuid = uuid;
        }

        public User()
        {
        }


        
    }

}