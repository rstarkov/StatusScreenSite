using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite.Services
{
    class PingService : ServiceBase<PingSettings, PingDto>
    {
        public override string ServiceName => "PingService";

        public PingService(Server server, PingSettings serviceSettings)
            : base(server, serviceSettings)
        {
        }

        public override void Start()
        {
            new Thread(thread) { IsBackground = true }.Start();
        }

        private void thread()
        {
            while (true)
            {
                var start = DateTime.UtcNow;
                try
                {
                    var ping = new Ping();
                    var response = ping.Send(Settings.Host, 2000);

                    var dto = new PingDto();
                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                    if (response.Status == IPStatus.Success)
                        dto.Last = (int) response.RoundtripTime;
                    else
                        dto.Last = null;

                    SendUpdate(dto);
                }
                catch
                {
                }

                Thread.Sleep(start.AddSeconds(5) - DateTime.UtcNow);
            }
        }
    }

    class PingSettings
    {
        public string Host = "8.8.8.8";
    }

    class PingDto : ITypescriptDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public int? Last;
    }
}
