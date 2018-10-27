using System;
using System.Diagnostics;
using System.Threading;
using RT.Util;
using RT.Util.Serialization;

namespace StatusScreenSite
{
    class Program
    {
        static Settings Settings;
        static Server Server;

        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--generate-dto")
            {
                TypescriptDto.GenerateTypescriptFile(args[1]);
                return 0;
            }
            else if (args.Length != 0)
                return 1;

            SettingsUtil.LoadSettings(out Settings);
            Server = new Server(Settings);
            Server.Start();
            using (var p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.Idle;
            while (true)
                Thread.Sleep(TimeSpan.FromMinutes(1));
        }
    }
}
