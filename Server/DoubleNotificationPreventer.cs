using System;
using System.Linq;
using System.Collections.Concurrent;

namespace hypixel
{
    public partial class NotificationService
    {
        /// <summary>
        /// Efficently prevents duble sending notification
        /// </summary>
        public class DoubleNotificationPreventer
        {

            private ConcurrentDictionary<int, ConcurrentQueue<int>> LastNotifications = new ConcurrentDictionary<int, ConcurrentQueue<int>>();

            public bool HasNeverBeenSeen(int userId, Notification not)
            {
                if (LastNotifications.TryGetValue(userId, out ConcurrentQueue<int> queue))
                {
                    if (queue.Contains(not.GetHashCode()))
                        return false;

                }
                else
                {
                    queue = new ConcurrentQueue<int>();
                    LastNotifications.AddOrUpdate(userId, queue, (id, queue) => queue);
                }
                queue.Enqueue(not.GetHashCode());
                if (queue.Count > 15)
                    queue.TryDequeue(out int a);
                return true;
            }
        }
    }
}