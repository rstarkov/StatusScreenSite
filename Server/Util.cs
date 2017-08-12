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
    }
}
