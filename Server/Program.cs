using System;
using System.Threading;
using RT.Util;

namespace StatusScreenSite
{
    class Program
    {
        static Settings Settings;
        static Server Server;

        static void Main(string[] args)
        {
            SettingsUtil.LoadSettings(out Settings);
            Server = new Server(Settings);
            Server.Start();
            while (true)
                Thread.Sleep(TimeSpan.FromMinutes(1));
        }
    }
}
