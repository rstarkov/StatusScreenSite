using System;
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

            Classify.DefaultOptions.AddTypeSubstitution(new QueueRouterHistoryPointSubstitutor());
            Classify.DefaultOptions.AddTypeSubstitution(new DictionaryDateTimeDecimalSubstitutor());
            Classify.DefaultOptions.AddTypeSubstitution(new QueueHttpingPointSubstitutor());
            Classify.DefaultOptions.AddTypeSubstitution(new QueueHttpingPointIntervalSubstitutor());

            SettingsUtil.LoadSettings(out Settings);
            Server = new Server(Settings);
            Server.Start();
            while (true)
                Thread.Sleep(TimeSpan.FromMinutes(1));
        }
    }
}
