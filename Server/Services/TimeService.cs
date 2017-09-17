using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace StatusScreenSite.Services
{
    class TimeService : ServiceBase<TimeSettings, TimeDto>
    {
        public override string ServiceName => "TimeService";
        private Queue<int?> _recentTimes = new Queue<int?>();

        public TimeService(Server server, TimeSettings serviceSettings)
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
                try
                {
                    var dto = new TimeDto();
                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromHours(24);
                    dto.LocalOffsetHours = getUtcOffset(Settings.LocalTimezoneName);
                    dto.TimeZones = Settings.ExtraTimezones.Select(tz => new TimeZoneDto { DisplayName = tz.DisplayName, OffsetHours = getUtcOffset(tz.TimezoneName) }).ToArray();

                    SendUpdate(dto);
                }
                catch
                {
                }

                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }

        private double getUtcOffset(string timezoneName)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneName).GetUtcOffset(DateTimeOffset.UtcNow).TotalHours;
        }
    }

    class TimeSettings
    {
        public string LocalTimezoneName = "GMT Standard Time";
        public List<TimeZone> ExtraTimezones = new List<TimeZone>();
    }

    class TimeZone
    {
        public string DisplayName = null;
        public string TimezoneName = null;
    }

    class TimeDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public double LocalOffsetHours { get; set; }
        public TimeZoneDto[] TimeZones { get; set; }
    }

    class TimeZoneDto
    {
        public string DisplayName { get; set; }
        public double OffsetHours { get; set; }
    }
}
