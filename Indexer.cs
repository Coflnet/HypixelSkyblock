using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Microsoft.EntityFrameworkCore;

namespace hypixel {
    public class Indexer {
        private static bool abort;
        private static bool minimumOutput;
        static int count = 0;

        public static void LastHourIndex () {
            DateTime indexStart;
            string targetTmp, pullPath;
            VariableSetup (out indexStart, out targetTmp, out pullPath);
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

            try {
                Console.WriteLine ("working");

                var work = PullData ();
                foreach (var item in work) {
                    ToDb (item);
                }
                Console.WriteLine ($"Indexing done, Indexed: {count} Saved: {StorageManager.SavedOnDisc} \tcache: {StorageManager.CacheItems}  NameRequests: {Program.RequestsSinceStart}");

                if (!abort)
                    // successful made this index save the startTime
                    FileController.SaveAs ("lastIndex", indexStart);
            } catch (System.AggregateException e) {
                // oh no an error occured
                Logger.Instance.Error ($"An error occured while indexing, abording: {e.Message} {e.StackTrace}");
                return;
                //FileController.DeleteFolder("auctionpull");

                //Directory.Move(FileController.GetAbsolutePath("awork"),FileController.GetAbsolutePath("auctionpull"));

            }
            var saveTask = StorageManager.Save ();
            ItemPrices.Instance.Save ();
            saveTask.Wait ();

            DeleteDir (targetTmp);
        }

        private static void VariableSetup (out DateTime indexStart, out string targetTmp, out string pullPath) {
            indexStart = DateTime.Now;
            Console.WriteLine ($"{indexStart}");
            var lastIndexStart = new DateTime (2020, 4, 25);
            if (FileController.Exists ("lastIndex"))
                lastIndexStart = FileController.LoadAs<DateTime> ("lastIndex");
            lastIndexStart = lastIndexStart - new TimeSpan (0, 20, 0);
            targetTmp = FileController.GetAbsolutePath ("awork");
            pullPath = FileController.GetAbsolutePath ("apull");
        }

        static IEnumerable<List<SaveAuction>> PullData () {
            var path = "awork";
            foreach (var item in FileController.FileNames ("*", path)) {
                if (abort) {
                    Console.WriteLine ("Stopped indexer");
                    yield break;
                }
                var fullPath = $"{path}/{item}";
                List<SaveAuction> data = null;
                try {
                    data = FileController.LoadAs<List<SaveAuction>> (fullPath);
                } catch (Exception) {
                    Console.WriteLine ("could not load downloaded auction-buffer");
                    FileController.Move (fullPath, "correupted/" + fullPath);
                }
                if (data != null) {
                    yield return data;
                    FileController.Delete (fullPath);
                }
            }
        }

        private static void ToDb (List<SaveAuction> auctions) {
            using (var context = new HypixelContext ()) {
                List<string> playerIds = new List<string> ();
                // preload
                var inDb = context.Auctions.Where (a => auctions.Select (oa => oa.Uuid)
                    .Contains (a.Uuid)).Include (a => a.Bids).ToDictionary (a => a.Uuid);
                BidComparer comparer = new BidComparer ();

                foreach (var auction in auctions) {

                    try {
                        playerIds.Add (auction.AuctioneerId);
                        foreach (var bid in auction.Bids) {
                            //Program.AddPlayer (context, bid.Bidder);
                            playerIds.Add (bid.Bidder);
                        }

                        var id = auction.Uuid;
                        if (inDb.TryGetValue (id, out SaveAuction dbauction)) {
                            foreach (var bid in auction.Bids) {

                                bid.Auction = dbauction;
                                if (!dbauction.Bids.Contains (bid, comparer)) {
                                    context.Bids.Add (bid);
                                    var shouldNotBeFalse = dbauction.Bids.Contains (bid, comparer);
                                    dbauction.HighestBidAmount = auction.HighestBidAmount;
                                }
                            }
                            // update
                            context.Auctions.Update (dbauction);
                        } else {
                            context.Auctions.Add (auction);
                        }

                        count++;
                        if (!minimumOutput && count % 5 == 0)
                            Console.Write ($"\r         Indexed: {count} Saved: {StorageManager.SavedOnDisc} \tcache: {StorageManager.CacheItems}  NameRequests: {Program.RequestsSinceStart}");

                    } catch (Exception e) {
                        Logger.Instance.Error ($"Error {e.Message} on {auction.ItemName} {auction.Uuid} from {auction.AuctioneerId}");
                        Logger.Instance.Error (e.StackTrace);
                    }

                }
                Program.AddPlayers (context, playerIds);

                context.SaveChanges ();
            }
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
                    var u = StorageManager.GetOrCreateUser (item.AuctioneerId, true);
                    u?.auctionIds.Add (item.Uuid);
                    // for search load the name
                    PlayerSearch.Instance.LoadName (u);
                }

            } catch (Exception e) {
                Console.WriteLine ("Corrupted " + item.AuctioneerId + $" {e.Message} \n{e.StackTrace}");
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

        internal static void MiniumOutput () {
            minimumOutput = true;
        }
    }
}