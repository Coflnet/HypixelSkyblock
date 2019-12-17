using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Coflnet;
using ConcurrentCollections;

namespace hypixel
{
    public class ItemPrices
    {
        public static ItemPrices Instance;

        private ConcurrentDictionary<string,ConcurrentHashSet<ItemIndexElement>> cache = new ConcurrentDictionary<string, ConcurrentHashSet<ItemIndexElement>>();

        private ConcurrentHashSet<string> pathsToSave = new ConcurrentHashSet<string>();

        static ItemPrices()
        {
            Instance = new ItemPrices();
        }

        public void AddAuction(SaveAuction auction)
        {
            var path = PathTo(auction.ItemName,auction.End);
            lock(path)
            {
                var auctions = ItemsForDaySet(auction.ItemName,auction.End);
                if(auctions == null)
                {
                    auctions = new ConcurrentHashSet<ItemIndexElement>();
                }
                var index = new ItemIndexElement(auction);
                
                if(auctions.Contains(index))
                {
                    auctions.TryRemove(index);
                }
                auctions.Add(index);
                Save(path,auctions);
            }
        }

        public IEnumerable<ItemIndexElement> Search(ItemSearchQuery search)
        {
            // round to full date in 2019
            if(search.Start < new DateTime(2019,6,6))
                search.Start = new DateTime(2019,6,6);
            search.Start = RoundDown(search.Start,TimeSpan.FromDays(1));


            // by day
            for (DateTime i = search.Start; i < search.End; i=i.AddDays(1))
            {
                foreach (var item in ItemsForDay(search.name,i))
                {
                    if(item.End < search.End && item.End > search.Start)
                        yield return item;
                }
            }
        }

        public IEnumerable<ItemIndexElement> ItemsForDay(string itemName,DateTime date)
        {
            return ItemsForDaySet(itemName,date);
        }

        private ConcurrentHashSet<ItemIndexElement> ItemsForDaySet(string itemName,DateTime date)
        {
            var path = PathTo(itemName,date);
            if(cache.TryGetValue(path,out ConcurrentHashSet<ItemIndexElement> auctions))
            {
                return auctions;
            }
            try{
                if(FileController.Exists(path))
                {
                    return FileController.LoadAs<ConcurrentHashSet<ItemIndexElement>>(path);
                } 

            } catch(System.InvalidOperationException)
            {
                Console.WriteLine("Failed to read "+ path);
            }
            

            return new ConcurrentHashSet<ItemIndexElement>();
        }

        private void Save(string path , ConcurrentHashSet<ItemIndexElement> auctions)
        {
            cache[path] = auctions;
            lock(pathsToSave){
                pathsToSave.Add(path);
            }
            if(pathsToSave.Count > 20000)
            {
                // start saving
                Save();
            }
            
        }

        public void Save()
        {
            lock(pathsToSave)
            {
                Console.Write($"\tSAVING {cache.Count}" );
                int count = 0;
                foreach (var item in pathsToSave)
                {
                    if(cache.TryRemove(item,out ConcurrentHashSet<ItemIndexElement> value))
                    {
                        FileController.SaveAs(item,value);
                        count++;
                    }
                }
                Console.Write($"\tSaved {count}");

                pathsToSave.Clear();
            }
        }


        string PathTo(string itemName, DateTime date)
        {
            return $"sitems/{ItemReferences.RemoveReforges(itemName)}/{date.ToString("yyyy-MM-dd")}";
        }

        public static DateTime RoundDown(DateTime date, TimeSpan span)
        {
            return new DateTime(date.Ticks / span.Ticks *span.Ticks);
        }
    }
}
