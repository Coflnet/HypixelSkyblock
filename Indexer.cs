using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Coflnet;
using dev;

namespace hypixel {
    public class Indexer {
        private static bool abort;
        public static void LastHourIndex () {
            var indexStart = DateTime.Now;
            Console.WriteLine ($"{indexStart}");
            var lastIndexStart = new DateTime (2020, 4, 25);
            if (FileController.Exists ("lastIndex"))
                lastIndexStart = FileController.LoadAs<DateTime> ("lastIndex");
                lastIndexStart = lastIndexStart-new TimeSpan(0,20,0);
            var targetTmp = FileController.GetAbsolutePath ("awork");
            var pullPath = FileController.GetAbsolutePath ("auctionpull");
            //DeleteDir(targetTmp);
            if (!Directory.Exists (pullPath) && !Directory.Exists (targetTmp)) {
                // update first
                Console.WriteLine ("nothing to build indexes from, run again with option u first");
                return;
            }
            // only copy the pull path if there is no temp work path yet
            if (!Directory.Exists (targetTmp))
                Directory.Move (pullPath, targetTmp);
            else
                Console.WriteLine ("Resuming work");

            int count = 0;
            try {
                Console.Write ("working");
                Parallel.ForEach (StorageManager.GetFileContents<SaveAuction> ("awork", false, true), (item, handler) => {
                    try {
                        if (abort) {
                            handler.Stop ();
                        }
                        if (item == null || item.Uuid == null) {
                            // we can't identify this auction, drop it
                            return;
                        }
                        StorageManager.GetOrCreateAuction (item.Uuid, item);
                        CreateIndex (item, false, lastIndexStart);
                        count++;
                        if (count % 5 == 0)
                            Console.Write ($"\r         Indexed: {count} Saved: {StorageManager.SavedOnDisc} \tcache: {StorageManager.CacheItems}  NameRequests: {Program.RequestsSinceStart}");

                    } catch (Exception e) {
                        Logger.Instance.Error ($"An exception was thrown {e.Message} {e.StackTrace}\n");
                    }
                });
                if (!abort)
                    // successful made this index save the startTime
                    FileController.SaveAs ("lastIndex", indexStart);
            } catch (System.AggregateException e) {
                // oh no an error occured
                Logger.Instance.Error ($"An error occured while indexing, abording: {e.Message} {e.StackTrace}");
                //FileController.DeleteFolder("auctionpull");

                //Directory.Move(FileController.GetAbsolutePath("awork"),FileController.GetAbsolutePath("auctionpull"));

            }
            var saveTask = StorageManager.Save ();
            ItemPrices.Instance.Save ();
            saveTask.Wait ();

            DeleteDir (targetTmp);
        }

        internal static void Stop () {
            abort = true;
        }

        public static void BuildIndexes () {
            Console.WriteLine ("building indexes");
            var lastIndex = new DateTime (1970, 1, 1);
            var updateStart = DateTime.Now;

            if (FileController.Exists ("lastIndex"))
                lastIndex = FileController.LoadAs<DateTime> ("lastIndex");

            // add an extra hour to make sure we don't miss something
            lastIndex = lastIndex.Subtract (new TimeSpan (1, 0, 0));

            AddIndexes (StorageManager.GetAllAuctions ());
            ItemPrices.Instance.Save ();

            // we are done
            FileController.SaveAs ("lastIndex", updateStart);
        }

        private static void AddIndexes (IEnumerable<SaveAuction> auctions) {
            int count = 0;
            Parallel.ForEach (auctions, (item, handler) => {
                if (abort) {
                    handler.Stop ();
                }
                if (item == null || item.Uuid == null) {
                    return;
                }

                CreateIndex (item, true);

                if (count++ % 10 == 0)
                    Console.Write ($"\r{count} {item.Uuid.Substring(0,5)} u{Program.usersLoaded}");
            });
            StorageManager.Save ().Wait ();
        }

        private static void CreateIndex (SaveAuction item, bool excludeUser = false, DateTime lastIndex = default (DateTime)) {
            if (item == null || item.ItemName == null) {
                // broken, ignore this aucion
                return;
            }
            try {
                //StorageManager.GetOrCreateItemRef(item.ItemName)?.auctions.Add(new ItemReferences.AuctionReference(item.Uuid,item.End));
                ItemPrices.Instance.AddAuction (item);
            } catch (Exception e) {
                Console.WriteLine ($"Error on {item.ItemName} {e.Message}");
                throw e;
            }

            if (excludeUser) {
                return;
            }

            try {
                if (item.Start > lastIndex) {
                    // we already have this
                    var u = StorageManager.GetOrCreateUser (item.Auctioneer, true);
                    u?.auctionIds.Add (item.Uuid);
                    // for search load the name
                    PlayerSearch.Instance.LoadName (u);
                }

            } catch (Exception e) {
                Console.WriteLine ("Corrupted " + item.Auctioneer + $" {e.Message} \n{e.StackTrace}");
            }

            foreach (var bid in item.Bids) {
                try {
                    if (bid.Timestamp < lastIndex) {
                        // we already have this
                        continue;
                    }
                    var u = StorageManager.GetOrCreateUser (bid.Bidder, true);
                    u.Bids.Add (new AuctionReference (null, item.Uuid));
                    PlayerSearch.Instance.LoadName (u);
                } catch (Exception e) {
                    Console.WriteLine ($"Corrupted user {bid.Bidder} {e.Message} {e.StackTrace}");
                    // removing it

                }

            }
        }

        private static void DeleteDir (string path) {
            if (!Directory.Exists (path)) {
                // nothing to do
                return;
            }

            System.IO.DirectoryInfo di = new DirectoryInfo (path);

            foreach (FileInfo file in di.GetFiles ()) {
                file.Delete ();
            }
            foreach (DirectoryInfo dir in di.GetDirectories ()) {
                dir.Delete (true);
            }
            Directory.Delete (path);
        }
    }
}