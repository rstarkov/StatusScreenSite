using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using RT.Util;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Serialization;

namespace StatusScreenSite.Services
{
    class HttpingService : ServiceBase<HttpingSettings, HttpingDto>
    {
        public override string ServiceName => "HttpingService";

        public HttpingService(Server server, HttpingSettings serviceSettings)
            : base(server, serviceSettings)
        {
        }

        public override void Start()
        {
            foreach (var tgt in Settings.Targets)
                tgt.Start();
            new Thread(thread) { IsBackground = true }.Start();
        }

        private void thread()
        {
            while (true)
            {
                var next = DateTime.UtcNow.AddSeconds(5);
                try
                {
                    //var dto = new HttpingDto();
                    //dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                    //SendUpdate(dto);
                }
                catch
                {
                }

                Util.SleepUntil(next);
            }
        }
    }

    class HttpingSettings
    {
        public List<HttpingTarget> Targets = new List<HttpingTarget> { new HttpingTarget() };
    }

    class HttpingTarget
    {
        public string Name = "Google";
        public string Url = "https://www.google.com";
        public TimeSpan Interval = TimeSpan.FromSeconds(5);
        public string MustContain = "";
        public string TimeZone = "GMT Standard Time";

        public QueueViewable<HttpingPoint> Recent = new QueueViewable<HttpingPoint>(); // must hold a month's worth in order to compute monthly percentiles
        public QueueViewable<HttpingPointInterval> Twominutely = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Hourly = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Daily = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Monthly = new QueueViewable<HttpingPointInterval>();

        [ClassifyIgnore]
        private TimeZoneInfo _timezone;

        public void Start()
        {
            _timezone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
            new Thread(thread) { IsBackground = true }.Start();
        }

        private void thread()
        {
            while (true)
            {
                var next = DateTime.UtcNow + Interval;
                try
                {
                    var hc = new HttpClient();
                    hc.Timeout = TimeSpan.FromSeconds(Interval.TotalSeconds * 0.90);

                    double msWait = -1;
                    double msDownload = -1;
                    bool error = false;
                    bool ok = false;
                    var start = DateTime.UtcNow;
                    try
                    {
                        var response = hc.GetAsync(Url).GetAwaiter().GetResult();
                        msWait = (DateTime.UtcNow - start).TotalMilliseconds;
                        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        msDownload = (DateTime.UtcNow - start).TotalMilliseconds - msWait;
                        if (response.StatusCode == System.Net.HttpStatusCode.OK && Encoding.UTF8.GetString(bytes).Contains(MustContain))
                            ok = true;
                    }
                    catch
                    {
                        error = true;
                    }

                    // Add this data point to Recent
                    var pt = new HttpingPoint { Timestamp = start.ToUnixSeconds() };
                    if (!error && msWait >= 0)
                        pt.MsWait = (ushort) ((int) msWait).Clip(1, 65535);
                    if (!error && ok && msDownload >= 0)
                        pt.MsDownload = (ushort) ((int) msDownload).Clip(1, 65535);
                    Recent.Enqueue(pt);
                    // Maintain the last 35 days in order to calculate monthly percentiles precisely
                    var cutoff = DateTime.UtcNow.AddDays(-35).ToUnixSeconds();
                    while (Recent.Count > 0 && Recent[0].Timestamp < cutoff)
                        Recent.Dequeue();

                    // Recalculate stats if we've crossed into the next minute
                    var prevPt = Recent.Count >= 2 ? Recent[Recent.Count - 2].Timestamp.FromUnixSeconds() : (DateTime?) null;
                    if (prevPt != null && prevPt.Value.TruncatedToMinutes() != start.TruncatedToMinutes())
                    {
                        AddIntervalIfRequired(Twominutely, prevPt.Value, start, dt => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (dt.Minute / 2) * 2, 0, DateTimeKind.Utc));
                        AddIntervalIfRequired(Hourly, prevPt.Value, start, dt => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc));
                        AddIntervalIfRequired(Daily, prevPt.Value, start, GetStartOfLocalDayInUtc);
                        AddIntervalIfRequired(Monthly, prevPt.Value, start, GetStartOfLocalMonthInUtc);
                    }

                    // Maintain the last 500 entries of each of these; monthly records are maintained forever
                    while (Twominutely.Count > 500)
                        Twominutely.Dequeue();
                    while (Hourly.Count > 500)
                        Hourly.Dequeue();
                    while (Daily.Count > 500)
                        Daily.Dequeue();
                }
                catch
                {
                }

                Util.SleepUntil(next);
            }
        }

        private DateTime GetStartOfLocalDayInUtc(DateTime dt)
        {
            var offset = _timezone.GetUtcOffset(dt);
            dt = dt + offset; // specified UTC time as local time
            dt = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc); // start of day in local time - with a UTC kind because we're about to make it UTC
            dt = dt - offset; // start of local day in UTC time
            return dt;
        }

        private DateTime GetStartOfLocalMonthInUtc(DateTime dt)
        {
            var offset = _timezone.GetUtcOffset(dt);
            dt = dt + offset; // specified UTC time as local time
            dt = new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc); // start of month in local time - with a UTC kind because we're about to make it UTC
            dt = dt - offset; // start of local month in UTC time
            return dt;
        }

        private void AddIntervalIfRequired(QueueViewable<HttpingPointInterval> queue, DateTime dtPrevUtc, DateTime dtCurUtc, Func<DateTime, DateTime> getIntervalStart)
        {
            var startPrevUtc = getIntervalStart(dtPrevUtc);
            var startCurUtc = getIntervalStart(dtCurUtc);
            if (startPrevUtc != startCurUtc)
                queue.Enqueue(ComputeStat(startPrevUtc, startCurUtc));
        }

        private HttpingPointInterval ComputeStat(DateTime startPrevUtc, DateTime startCurUtc)
        {
            var startPrevTs = startPrevUtc.ToUnixSeconds();
            var startCurTs = startCurUtc.ToUnixSeconds();
            var msWait = new List<ushort>();
            var msDownload = new List<ushort>();
            var interval = new HttpingPointInterval { StartUtc = startPrevUtc };
            for (int i = Recent.Count - 2 /* because the last point is known to be in the new interval */; i >= 0; i--)
            {
                var pt = Recent[i];
                if (pt.Timestamp < startPrevTs)
                    break; // none of the other points will be within this interval
                if (pt.Timestamp >= startCurTs)
                    continue; // should never trigger but in case this method is called in other circumstances...
                interval.TotalCount++;
                if (pt.MsWait == 0 && pt.MsDownload == 0)
                    interval.TimeoutCount++;
                else if (pt.MsDownload == 0)
                    interval.ErrorCount++;
                else
                {
                    msWait.Add(pt.MsWait);
                    msDownload.Add(pt.MsDownload);
                }
            }
            Ut.Assert(msWait.Count == msDownload.Count);
            Ut.Assert(interval.TotalCount == interval.TimeoutCount + interval.ErrorCount + msWait.Count);
            if (msWait.Count > 0)
            {
                msWait.Sort();
                msDownload.Sort();
                SetPercentiles(ref interval.MsWait, msWait);
                SetPercentiles(ref interval.MsDownload, msDownload);
            }
            return interval;
        }

        private void SetPercentiles(ref HttpingStatistic stat, List<ushort> sortedValues)
        {
            stat.Prc01 = sortedValues[(sortedValues.Count - 1) * 1 / 100];
            stat.Prc25 = sortedValues[(sortedValues.Count - 1) * 25 / 100];
            stat.Prc50 = sortedValues[(sortedValues.Count - 1) * 50 / 100];
            stat.Prc75 = sortedValues[(sortedValues.Count - 1) * 75 / 100];
            stat.Prc95 = sortedValues[(sortedValues.Count - 1) * 95 / 100];
            stat.Prc99 = sortedValues[(sortedValues.Count - 1) * 99 / 100];
        }
    }

    class HttpingDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
    }

    /// <summary>
    ///     MsWait = 0 + MsDownload = 0 means timeout. If only MsDownload is 0 then it was a response we consider an error
    ///     (wrong status code; expected text missing).</summary>
    struct HttpingPoint
    {
        public uint Timestamp; // seconds since 1970-01-01 00:00:00 UTC
        public ushort MsWait;
        public ushort MsDownload;

        public override string ToString() => $"{Timestamp.FromUnixSeconds()} : {MsWait}+{MsDownload}";
    }

    struct HttpingStatistic
    {
        public ushort Prc01;
        public ushort Prc25;
        public ushort Prc50;
        public ushort Prc75;
        public ushort Prc95;
        public ushort Prc99;

        public override string ToString() => $"{Prc01} / {Prc50} / {Prc99}";
    }

    struct HttpingPointInterval
    {
        public DateTime StartUtc; // UTC timestamp of the beginning of this interval
        public HttpingStatistic MsWait; // timeouts and errors are not included
        public HttpingStatistic MsDownload;
        public int TotalCount;
        public int TimeoutCount;
        public int ErrorCount;

        public override string ToString() => $"{StartUtc} : {TotalCount:#,0} samples, {TimeoutCount + ErrorCount:#,0} timeouts/errors, {MsWait}";
    }
}
