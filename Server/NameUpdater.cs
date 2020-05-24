using System;
using System.Linq;
using System.Threading.Tasks;
using dev;

namespace hypixel {
    public class NameUpdater {
        public static DateTime LastUpdate { get; internal set; }

        public static void UpdateHundredNames () {
            using (var context = new HypixelContext ()) {
                var players = context.Players.Where (p => p.Name == null).Take (100);

                if (players.Count () == 0) {
                    players = context.Players.OrderBy (p => p.UpdatedAt).Take (100);
                }

                foreach (var player in players) {
                    player.Name = Program.GetPlayerNameFromUuid (player.UuId);
                    if(player.Name == null)
                    {
                        // this is not what we wanted
                        continue;
                    }
                    context.Players.Update (player);
                }

                context.SaveChanges ();
            }
            LastUpdate = DateTime.Now;
        }

        public static void Run () {
            Task.Run (() => {
                RunForever ();
            });
        }

        static void RunForever () {
            while (true) {
                try {
                    UpdateHundredNames ();
                    Console.WriteLine (" - Updated hundret player names - ");
                } catch (Exception e) {
                    Logger.Instance.Error ($"NameUpdater encountered an error \n {e.Message} {e.StackTrace} \n{e.InnerException?.Message} {e.InnerException?.StackTrace}");
                }
                    System.Threading.Thread.Sleep (10000);
            }
        }
    }

}