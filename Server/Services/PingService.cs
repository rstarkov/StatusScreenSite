using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;

namespace StatusScreenSite.Services
{
    class PingService : ServiceBase<PingSettings, PingDto>
    {
        public override string ServiceName => "PingService";
        private Queue<int?> _recentPings = new Queue<int?>();

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
                        dto.Last = (int) Math.Min(response.RoundtripTime, 2000);
                    else
                        dto.Last = null;

                    _recentPings.Enqueue(dto.Last);
                    while (_recentPings.Count > 24)
                        _recentPings.Dequeue();
                    dto.Recent = _recentPings.ToArray();

                    SendUpdate(dto);
                }
                catch
                {
                }

                Util.SleepUntil(start.AddSeconds(5));
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
        public int?[] Recent;
    }
}
