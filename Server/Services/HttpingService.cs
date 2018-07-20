using System;
using System.Collections.Generic;
using System.Linq;
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
                    var dto = new HttpingDto();
                    dto.Targets = new HttpingTargetDto[Settings.Targets.Count];
                    int i = 0;
                    foreach (var tgt in Settings.Targets)
                    {
                        lock (tgt.Lock)
                        {
                            var cutoff30m = DateTime.UtcNow.AddMinutes(-30).ToUnixSeconds();
                            var cutoff24h = DateTime.UtcNow.AddHours(-24).ToUnixSeconds();
                            var cutoff30d = DateTime.UtcNow.AddDays(-30).ToUnixSeconds();
                            var stamps30m = new List<ushort>();
                            var stamps24h = new List<ushort>();
                            var stamps30d = new List<ushort>();
                            var last30m = new HttpingIntervalDto();
                            var last24h = new HttpingIntervalDto();
                            var last30d = new HttpingIntervalDto();
                            for (int k = tgt.Recent.Count - 1; k >= 0; k--)
                            {
                                var pt = tgt.Recent[k];
                                if (pt.Timestamp > cutoff30m)
                                {
                                    last30m.TotalCount++;
                                    if (pt.MsResponse == 0)
                                        last30m.ErrorCount++;
                                    else if (pt.MsResponse == 65535)
                                        last30m.TimeoutCount++;
                                    else
                                        stamps30m.Add(pt.MsResponse);
                                }
                                if (pt.Timestamp > cutoff24h)
                                {
                                    last24h.TotalCount++;
                                    if (pt.MsResponse == 0)
                                        last24h.ErrorCount++;
                                    else if (pt.MsResponse == 65535)
                                        last24h.TimeoutCount++;
                                    else
                                        stamps24h.Add(pt.MsResponse);
                                }
                                if (pt.Timestamp > cutoff30d)
                                {
                                    last30d.TotalCount++;
                                    if (pt.MsResponse == 0)
                                        last30d.ErrorCount++;
                                    else if (pt.MsResponse == 65535)
                                        last30d.TimeoutCount++;
                                    else
                                        stamps30d.Add(pt.MsResponse);
                                }
                            }
                            stamps30m.Sort();
                            stamps24h.Sort();
                            stamps30d.Sort();
                            SetPercentiles(ref last30m, stamps30m);
                            SetPercentiles(ref last24h, stamps24h);
                            SetPercentiles(ref last30d, stamps30d);

                            var tgtdto = new HttpingTargetDto
                            {
                                Name = tgt.Name,
                                Twominutely = GetIntervalDto(tgt.Twominutely, TimeSpan.FromMinutes(2), tgt.GetStartOfTwominute),
                                Hourly = GetIntervalDto(tgt.Hourly, TimeSpan.FromMinutes(2), tgt.GetStartOfHour),
                                Daily = GetIntervalDto(tgt.Daily, TimeSpan.FromMinutes(2), tgt.GetStartOfLocalDayInUtc),
                                Monthly = GetIntervalDto(tgt.Monthly, TimeSpan.FromMinutes(2), tgt.GetStartOfLocalMonthInUtc),
                                Last30m = last30m,
                                Last24h = last24h,
                                Last30d = last30d,
                            };
                            //tgtdto.Recent = ???;

                            dto.Targets[i] = tgtdto;
                        }
                        i++;
                    }

                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                    SendUpdate(dto);
                }
                catch
                {
                }

                Util.SleepUntil(next);
            }
        }

        private void SetPercentiles(ref HttpingIntervalDto stat, List<ushort> sortedValues)
        {
            stat.MsResponsePrc01 = sortedValues[(sortedValues.Count - 1) * 1 / 100];
            stat.MsResponsePrc25 = sortedValues[(sortedValues.Count - 1) * 25 / 100];
            stat.MsResponsePrc50 = sortedValues[(sortedValues.Count - 1) * 50 / 100];
            stat.MsResponsePrc75 = sortedValues[(sortedValues.Count - 1) * 75 / 100];
            stat.MsResponsePrc95 = sortedValues[(sortedValues.Count - 1) * 95 / 100];
            stat.MsResponsePrc99 = sortedValues[(sortedValues.Count - 1) * 99 / 100];
        }

        private HttpingIntervalDto[] GetIntervalDto(QueueViewable<HttpingPointInterval> data, TimeSpan interval, Func<DateTime, DateTime> getIntervalStart)
        {
            const int count = 30;
            var cur = getIntervalStart(DateTime.UtcNow) - interval;
            var result = new List<HttpingIntervalDto>();
            for (int i = data.Count - 1; i >= 0; i--)
            {
                var pt = data[i];
                if (pt.StartUtc > cur)
                    continue;
                while (pt.StartUtc < cur && result.Count < count)
                {
                    result.Add(new HttpingIntervalDto { TotalCount = 0 });
                    cur -= interval;
                }
                if (result.Count >= count)
                    break;
                Ut.Assert(pt.StartUtc == cur);
                result.Add(new HttpingIntervalDto(pt));
                cur -= interval;
            }
            while (result.Count < count)
                result.Add(new HttpingIntervalDto { TotalCount = 0 });
            while (result.Count > count)
                result.RemoveRange(count, result.Count - count);
            result.Reverse();
            return result.ToArray();
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
        [ClassifyIgnore]
        public object Lock = new object();

        public override string ToString() => $"{Name} ({Url}) : {Recent.Count:#,0} recent, {Twominutely.Count:#,0} twomin, {Hourly.Count:#,0} hourly, {Daily.Count:#,0} daily, {Monthly.Count:#,0} monthly";

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

                    double msResponse = -1;
                    bool error = false;
                    bool ok = false;
                    var start = DateTime.UtcNow;
                    try
                    {
                        var response = hc.GetAsync(Url).GetAwaiter().GetResult();
                        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        msResponse = (DateTime.UtcNow - start).TotalMilliseconds;
                        if (response.StatusCode == System.Net.HttpStatusCode.OK && Encoding.UTF8.GetString(bytes).Contains(MustContain))
                            ok = true;
                    }
                    catch
                    {
                        error = true;
                    }

                    lock (Lock)
                    {
                        // Add this data point to Recent
                        var pt = new HttpingPoint { Timestamp = start.ToUnixSeconds() };
                        if (error)
                            pt.MsResponse = 65535; // timeout
                        else if (!ok)
                            pt.MsResponse = 0; // wrong code or didn't contain what we wanted
                        else
                            pt.MsResponse = (ushort) ((int) Math.Round(msResponse)).Clip(1, 65534);
                        Recent.Enqueue(pt);
                        // Maintain the last 35 days in order to calculate monthly percentiles precisely
                        var cutoff = DateTime.UtcNow.AddDays(-35).ToUnixSeconds();
                        while (Recent.Count > 0 && Recent[0].Timestamp < cutoff)
                            Recent.Dequeue();

                        // Recalculate stats if we've crossed into the next minute
                        var prevPt = Recent.Count >= 2 ? Recent[Recent.Count - 2].Timestamp.FromUnixSeconds() : (DateTime?) null;
                        if (prevPt != null && prevPt.Value.TruncatedToMinutes() != start.TruncatedToMinutes())
                        {
                            AddIntervalIfRequired(Twominutely, prevPt.Value, start, GetStartOfTwominute);
                            AddIntervalIfRequired(Hourly, prevPt.Value, start, GetStartOfHour);
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
                }
                catch
                {
                }

                Util.SleepUntil(next);
            }
        }

        public DateTime GetStartOfTwominute(DateTime dt) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (dt.Minute / 2) * 2, 0, DateTimeKind.Utc);
        public DateTime GetStartOfHour(DateTime dt) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);

        public DateTime GetStartOfLocalDayInUtc(DateTime dt)
        {
            var offset = _timezone.GetUtcOffset(dt);
            dt = dt + offset; // specified UTC time as local time
            dt = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc); // start of day in local time - with a UTC kind because we're about to make it UTC
            dt = dt - offset; // start of local day in UTC time
            return dt;
        }

        public DateTime GetStartOfLocalMonthInUtc(DateTime dt)
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
            var msResponse = new List<ushort>();
            var interval = new HttpingPointInterval { StartUtc = startPrevUtc };
            for (int i = Recent.Count - 2 /* because the last point is known to be in the new interval */; i >= 0; i--)
            {
                var pt = Recent[i];
                if (pt.Timestamp < startPrevTs)
                    break; // none of the other points will be within this interval
                if (pt.Timestamp >= startCurTs)
                    continue; // should never trigger but in case this method is called in other circumstances...
                interval.TotalCount++;
                if (pt.MsResponse == 65535)
                    interval.TimeoutCount++;
                else if (pt.MsResponse == 0)
                    interval.ErrorCount++;
                else
                {
                    msResponse.Add(pt.MsResponse);
                }
            }
            Ut.Assert(interval.TotalCount == interval.TimeoutCount + interval.ErrorCount + msResponse.Count);
            if (msResponse.Count > 0)
            {
                msResponse.Sort();
                SetPercentiles(ref interval.MsResponse, msResponse);
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

    struct HttpingPoint
    {
        public uint Timestamp; // seconds since 1970-01-01 00:00:00 UTC
        public ushort MsResponse; // 65535 = timeout; 0 = error (wrong status code or expected text missing)

        public override string ToString() => $"{Timestamp.FromUnixSeconds()} : {MsResponse:#,0} ms";
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
        public HttpingStatistic MsResponse; // timeouts and errors are not included
        public int TotalCount;
        public int TimeoutCount;
        public int ErrorCount;

        public override string ToString() => $"{StartUtc} : {TotalCount:#,0} samples, {TimeoutCount + ErrorCount:#,0} timeouts/errors, {MsResponse}";
    }

    class HttpingDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public HttpingTargetDto[] Targets { get; set; }
    }

    struct HttpingIntervalDto
    {
        public ushort MsResponsePrc01 { get; set; }
        public ushort MsResponsePrc25 { get; set; }
        public ushort MsResponsePrc50 { get; set; }
        public ushort MsResponsePrc75 { get; set; }
        public ushort MsResponsePrc95 { get; set; }
        public ushort MsResponsePrc99 { get; set; }
        public int TotalCount { get; set; } // 0 = missing data
        public int TimeoutCount { get; set; }
        public int ErrorCount { get; set; }

        public HttpingIntervalDto(HttpingPointInterval pt) : this()
        {
            TotalCount = pt.TotalCount;
            TimeoutCount = pt.TimeoutCount;
            ErrorCount = pt.ErrorCount;
            MsResponsePrc01 = pt.MsResponse.Prc01;
            MsResponsePrc25 = pt.MsResponse.Prc25;
            MsResponsePrc50 = pt.MsResponse.Prc50;
            MsResponsePrc75 = pt.MsResponse.Prc75;
            MsResponsePrc95 = pt.MsResponse.Prc95;
            MsResponsePrc99 = pt.MsResponse.Prc99;
        }
    }

    class HttpingTargetDto
    {
        public string Name { get; set; }
        public int[] Recent { get; set; } // 0 = error, 65535 = timeout, -1 = missing data
        public HttpingIntervalDto[] Twominutely { get; set; }
        public HttpingIntervalDto[] Hourly { get; set; }
        public HttpingIntervalDto[] Daily { get; set; }
        public HttpingIntervalDto[] Monthly { get; set; }
        public HttpingIntervalDto Last30m { get; set; }
        public HttpingIntervalDto Last24h { get; set; }
        public HttpingIntervalDto Last30d { get; set; }
    }
}
