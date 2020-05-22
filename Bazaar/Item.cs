using System.Collections.Generic;

namespace dev
{
    public class Item
    {
        public short Id {get;set;}
        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        public string Tag {get;set;}

        List<ItemName> Names {get;set;}

        public string Description {get;set;}
    }
}