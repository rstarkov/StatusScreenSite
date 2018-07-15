using System;
using System.Threading;

namespace StatusScreenSite
{
    static class Util
    {
        public static void SleepUntil(DateTime time)
        {
            var duration = time.ToUniversalTime() - DateTime.UtcNow;
            if (duration > TimeSpan.Zero)
                Thread.Sleep(duration);
        }

        private static DateTime _unixepoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static uint ToUnixSeconds(this DateTime time)
        {
            return (uint) (time - _unixepoch).TotalSeconds;
        }

        public static DateTime FromUnixSeconds(this uint time)
        {
            return _unixepoch.AddSeconds(time);
        }
    }
}
