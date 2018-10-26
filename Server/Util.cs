using System;
using System.IO;
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

        public static string GetNonexistentFileName(Func<int, string> getFileName)
        {
            string newname;
            for (int num = 0; ; num++)
                if (!File.Exists(newname = getFileName(num)))
                    return newname;
        }

        public static long ToDbDateTime(this DateTime dt) => (long) (dt.ToUniversalTime() - _unixepoch).TotalMilliseconds;
        public static long? ToDbDateTime(this DateTime? dt) => dt?.ToDbDateTime() ?? null;
    }
}
