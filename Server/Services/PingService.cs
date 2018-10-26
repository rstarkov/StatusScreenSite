using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Dapper;
using Dapper.Contrib.Extensions;

namespace StatusScreenSite.Services
{
    class PingService : ServiceBase<PingSettings, PingDto>
    {
        public override string ServiceName => "PingService";
        private Queue<(int? ms, DateTime utc)> _recentPings = new Queue<(int? ms, DateTime utc)>();
        public IEnumerable<(int? ms, DateTime utc)> RecentPings => _recentPings.AsEnumerable();

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
                    var utc = DateTime.UtcNow;
                    dto.ValidUntilUtc = utc + TimeSpan.FromSeconds(15);
                    if (response.Status == IPStatus.Success)
                        dto.Last = (int) Math.Min(response.RoundtripTime, 2000);
                    else
                        dto.Last = null;

                    using (var db = Db.Open())
                        db.Insert(new TbPingHistoryEntry { Timestamp = utc.ToDbDateTime(), Ping = dto.Last });

                    _recentPings.Enqueue((dto.Last, utc));
                    while (_recentPings.Count > 24)
                        _recentPings.Dequeue();
                    dto.Recent = _recentPings.Select(t => t.ms).ToArray();

                    SendUpdate(dto);
                }
                catch
                {
                }

                Util.SleepUntil(start.AddSeconds(5));
            }
        }

        public override bool MigrateSchema(SQLiteConnection db, int curVersion)
        {
            if (curVersion == 0)
            {
                db.Execute($@"CREATE TABLE {nameof(TbPingHistoryEntry)} (
                    {nameof(TbPingHistoryEntry.Timestamp)} INTEGER PRIMARY KEY,
                    {nameof(TbPingHistoryEntry.Ping)} INT NULL
                )");
                return true;
            }

            return false;
        }
    }

    class PingSettings
    {
        public string Host = "8.8.8.8";
    }

    class PingDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public int? Last { get; set; }
        public int?[] Recent { get; set; }
    }

    class TbPingHistoryEntry
    {
        [ExplicitKey]
        public long Timestamp { get; set; }
        public int? Ping { get; set; }
    }
}
