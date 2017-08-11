using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StatusScreenSite
{
    static class Util
    {
        private static object start;

        public static void SleepUntil(DateTime time)
        {
            var duration = time.ToUniversalTime() - DateTime.UtcNow;
            if (duration > TimeSpan.Zero)
                Thread.Sleep(duration);
        }
    }
}
