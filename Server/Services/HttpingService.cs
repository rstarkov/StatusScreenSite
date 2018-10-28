using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Dapper;
using Dapper.Contrib.Extensions;
using RT.Servers;
using RT.Util;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite.Services
{
    class HttpingService : ServiceBase<HttpingSettings, HttpingDto>
    {
        public override string ServiceName => "HttpingService";

        private PingService _pingSvc;
        private List<HttpingTarget> _targets;

        public HttpingService(Server server, HttpingSettings serviceSettings, PingService pingSvc)
            : base(server, serviceSettings)
        {
            _pingSvc = pingSvc;
            server.AddUrlMapping(new UrlMapping(new UrlHook(path: "/HttpingService/ChartSvg", specificPath: true), handleChartSvg));
        }

        public override void Start()
        {
            _targets = Settings.Targets.Select(ts => new HttpingTarget { Settings = ts }).ToList();
            foreach (var tgt in _targets)
                tgt.Start(this);
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
                    dto.Targets = new HttpingTargetDto[_targets.Count];
                    int i = 0;
                    foreach (var tgt in _targets)
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
                                if (pt.Timestamp > cutoff30m && last30m.CountSample(pt.MsResponse))
                                    stamps30m.Add(pt.MsResponse);
                                if (pt.Timestamp > cutoff24h && last24h.CountSample(pt.MsResponse))
                                    stamps24h.Add(pt.MsResponse);
                                if (pt.Timestamp > cutoff30d && last30d.CountSample(pt.MsResponse))
                                    stamps30d.Add(pt.MsResponse);
                            }
                            stamps30m.Sort();
                            stamps24h.Sort();
                            stamps30d.Sort();
                            SetPercentiles(ref last30m, stamps30m);
                            SetPercentiles(ref last24h, stamps24h);
                            SetPercentiles(ref last30d, stamps30d);

                            var tgtdto = new HttpingTargetDto
                            {
                                Name = tgt.Settings.Name,
                                Twominutely = GetIntervalDto(tgt.Twominutely, TimeSpan.FromMinutes(2), tgt.GetStartOfTwominute),
                                Hourly = GetIntervalDto(tgt.Hourly, TimeSpan.FromHours(1), tgt.GetStartOfHour),
                                Daily = GetIntervalDto(tgt.Daily, TimeSpan.FromHours(24), tgt.GetStartOfLocalDayInUtc),
                                Monthly = GetIntervalDto(tgt.Monthly, TimeSpan.FromDays(30), tgt.GetStartOfLocalMonthInUtc),
                                Last30m = last30m,
                                Last24h = last24h,
                                Last30d = last30d,
                            };
                            tgtdto.Recent = tgt.Recent.Select(pt => (int) pt.MsResponse).Skip(tgt.Recent.Count - 30).ToArray();

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
            stat.MsResponsePrc01 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 1 / 100];
            stat.MsResponsePrc25 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 25 / 100];
            stat.MsResponsePrc50 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 50 / 100];
            stat.MsResponsePrc75 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 75 / 100];
            stat.MsResponsePrc95 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 95 / 100];
            stat.MsResponsePrc99 = sortedValues.Count == 0 ? (ushort) 0 : sortedValues[(sortedValues.Count - 1) * 99 / 100];
        }

        private HttpingIntervalDto[] GetIntervalDto(QueueViewable<HttpingPointInterval> data, TimeSpan interval, Func<DateTime, DateTime> getIntervalStart)
        {
            const int count = 30;
            var cur = getIntervalStart(getIntervalStart(DateTime.UtcNow) - interval);
            var result = new List<HttpingIntervalDto>();
            for (int i = data.Count - 1; i >= 0; i--)
            {
                var pt = data[i];
                if (pt.StartUtc > cur)
                    continue;
                while (pt.StartUtc < cur && result.Count < count)
                {
                    result.Add(new HttpingIntervalDto { TotalCount = 0 });
                    cur = getIntervalStart(cur - interval);
                }
                if (result.Count >= count)
                    break;
                Ut.Assert(pt.StartUtc == cur);
                result.Add(new HttpingIntervalDto(pt));
                cur = getIntervalStart(cur - interval);
            }
            while (result.Count < count)
                result.Add(new HttpingIntervalDto { TotalCount = 0 });
            while (result.Count > count)
                result.RemoveRange(count, result.Count - count);
            result.Reverse();
            return result.ToArray();
        }

        public bool IsGoodInternetConnection()
        {
            // is ok if we have at least 4 pings in the last 30s, all of which are under 35 ms
            var pings = _pingSvc.RecentPings.Where(p => p.utc >= DateTime.UtcNow.AddSeconds(-30));
            return pings.Count() >= 4 && pings.All(p => p.ms != null && p.ms < 35);
        }

        public override bool MigrateSchema(SQLiteConnection db, int curVersion)
        {
            if (curVersion == 0)
            {
                db.Execute(@"CREATE TABLE TbHttpingSite (
                    SiteId INTEGER PRIMARY KEY,
                    InternalName TEXT NOT NULL
                )");

                db.Execute(@"CREATE TABLE TbHttpingRecent (
                    SiteId BIGINT NOT NULL,
                    Timestamp BIGINT NOT NULL,
                    MsResponse INT NOT NULL,
                    PRIMARY KEY (SiteId, Timestamp)
                )");

                db.Execute(@"CREATE TABLE TbHttpingInterval (
                    SiteId BIGINT NOT NULL,
                    StartTimestamp BIGINT NOT NULL,
                    IntervalLength INT NOT NULL,

                    TotalCount INT NOT NULL,
                    TimeoutCount INT NOT NULL,
                    ErrorCount INT NOT NULL,

                    MsResponsePrc01 INT NOT NULL,
                    MsResponsePrc25 INT NOT NULL,
                    MsResponsePrc50 INT NOT NULL,
                    MsResponsePrc75 INT NOT NULL,
                    MsResponsePrc95 INT NOT NULL,
                    MsResponsePrc99 INT NOT NULL,

                    PRIMARY KEY (SiteId, StartTimestamp, IntervalLength)
                )");

                return true;
            }

            return false;
        }

        private static void mergeRecentFromAnotherDb(string otherDbPath)
        {
            using (var db = Db.Open())
            using (var dbOther = new SQLiteConnection($"Data Source={otherDbPath};Version=3;").OpenAndReturn())
            {
                var sitesTheirs = dbOther.Query<TbHttpingSite>($"SELECT * FROM {nameof(TbHttpingSite)} WHERE {nameof(TbHttpingSite.SiteId)} IN (SELECT DISTINCT {nameof(TbHttpingRecent.SiteId)} FROM {nameof(TbHttpingRecent)})")
                    .ToDictionary(s => s.SiteId, s => s.InternalName);
                var sitesOurs = db.GetAll<TbHttpingSite>().ToDictionary(s => s.InternalName, s => s.SiteId);
                var sites = sitesTheirs.Select(th => new { theirSiteId = th.Key, ourSiteId = sitesOurs[th.Value] }).ToList();

                foreach (var s in sites)
                {
                    Console.WriteLine($"Loading data for site {s.ourSiteId}...");

                    var ours = db.Query<TbHttpingRecent>($"SELECT * FROM {nameof(TbHttpingRecent)} WHERE {nameof(TbHttpingRecent.SiteId)} = @siteId", new { siteId = s.ourSiteId }).ToDictionary(x => x.Timestamp, x => x.MsResponse);
                    var theirs = dbOther.Query<TbHttpingRecent>($"SELECT * FROM {nameof(TbHttpingRecent)} WHERE {nameof(TbHttpingRecent.SiteId)} = @siteId", new { siteId = s.theirSiteId }).ToDictionary(x => x.Timestamp, x => x.MsResponse);

                    Console.WriteLine($"Comparing and inserting missing records...");
                    using (var trn = db.BeginTransaction())
                    {
                        int inserted = 0;
                        foreach (var ts in theirs.Keys)
                        {
                            if (ours.ContainsKey(ts))
                            {
                                if (theirs[ts] != ours[ts])
                                    Console.WriteLine($"Difference found: {s.ourSiteId} at {ts.FromDbDateTime()}: {theirs[ts]} != {ours[ts]}");
                            }
                            else
                            {
                                db.Insert(new TbHttpingRecent { SiteId = s.ourSiteId, Timestamp = ts, MsResponse = theirs[ts] }, trn);
                                inserted++;
                            }
                        }
                        Console.WriteLine($"Inserted {inserted:#,0} records for site ID {s.ourSiteId}");
                        trn.Commit();
                    }
                }
            }
        }

        private HttpResponse handleChartSvg(HttpRequest request)
        {
            var siteName = request.Url["server"] ?? throw new HttpNotFoundException();
            var interval = EnumStrong.Parse<HttpingIntervalLength>(request.Url["interval"]);
            var tsFrom = long.Parse(request.Url["from"]);
            var tsTo = long.Parse(request.Url["to"]);
            var maxOverride = request.Url["max"] == null ? (int?) null : int.Parse(request.Url["max"]);
            var logY = request.Url["log"] == "1";
            var percentiles = request.Url.QueryValues("prc").ToHashSet(); // if none: plot uptime instead
            var barH = request.Url["barH"] == null ? 80.0 : double.Parse(request.Url["barH"]);
            var barW = request.Url["barW"] == null ? 10.0 : double.Parse(request.Url["barW"]);
            var barGap = barW * (request.Url["barGap"] == null ? 0.2 : double.Parse(request.Url["barGap"]));

            var margin = 10.0;

            var clrGreenBar = "#08b025";
            var clrGreenDarkBar = "#057519";
            var clrYellowBar = "#ffff00";
            var clrBlueBar = "#1985f3";
            var clrBlueDarkBar = "#0959aa";
            var clrRedBar = "#ff0000";
            var clrFuchsiaBar = "#ff00ff";
            var clrGreyBar = "#404040";

            using (var db = Db.Open())
            {
                var site = db.QuerySingle<TbHttpingSite>($"SELECT * FROM {nameof(TbHttpingSite)} WHERE {nameof(TbHttpingSite.InternalName)} = @siteName", new { siteName }) ?? throw new HttpNotFoundException();
                var tgt = _targets.Single(t => t.Settings.InternalName == siteName);
                var points = db.Query<TbHttpingInterval>($@"
                    SELECT * FROM {nameof(TbHttpingInterval)}
                    WHERE {nameof(TbHttpingInterval.SiteId)} = @SiteId
                        AND {nameof(TbHttpingInterval.IntervalLength)} = @interval
                        AND {nameof(TbHttpingInterval.StartTimestamp)} >= @tsFrom
                        AND {nameof(TbHttpingInterval.StartTimestamp)} < @tsTo
                    ORDER BY {nameof(TbHttpingInterval.StartTimestamp)}
                    ",
                    new { site.SiteId, interval, tsFrom, tsTo }).ToList();

                var intervalIncrement = interval == HttpingIntervalLength.TwoMinutes ? TimeSpan.FromMinutes(2) : interval == HttpingIntervalLength.Hour ? TimeSpan.FromHours(1)
                    : interval == HttpingIntervalLength.Day ? TimeSpan.FromHours(24 + 4 /* for DST changes */) : interval == HttpingIntervalLength.Month ? TimeSpan.FromDays(35) : throw new Exception();
                var startOfInterval = interval == HttpingIntervalLength.TwoMinutes ? tgt.GetStartOfTwominute : interval == HttpingIntervalLength.Hour ? tgt.GetStartOfHour
                    : interval == HttpingIntervalLength.Day ? tgt.GetStartOfLocalDayInUtc : interval == HttpingIntervalLength.Month ? (Func<DateTime, DateTime>) tgt.GetStartOfLocalMonthInUtc : throw new Exception();
                var max = maxOverride ?? points.Max(pt => pt.MsResponsePrc50);
                double curX = margin;
                var curInterval = points[0].StartTimestamp.FromDbDateTime();
                var lastInterval = points[points.Count - 1].StartTimestamp.FromDbDateTime();

                var sb = new StringBuilder();
                void addBar(double yBot, double yTop, string color)
                {
                    if (yTop <= 0 || yBot > max)
                        return;
                    if (yBot < 0) yBot = 0;
                    if (yTop > max) yTop = max;
                    sb.Append($"<rect x='{curX}' y='{margin + barH - yTop / max * barH}' width='{barW}' height='{(yTop - yBot) / max * barH}' stroke-width='0' fill='{color}'></rect>");
                }

                int p = 0;
                for (; curInterval <= lastInterval; curInterval = startOfInterval(curInterval + intervalIncrement))
                {
                    var pt = points[p];
                    if (pt.StartTimestamp.FromDbDateTime() != curInterval)
                    {
                        addBar(0, max, clrGreyBar);
                    }
                    else
                    {
                        if (percentiles.Count == 0)
                        {
                            addBar(0, 1, pt.TimeoutCount + pt.ErrorCount == 0 ? clrGreenBar : clrGreenDarkBar);
                            addBar(0, (pt.TimeoutCount + pt.ErrorCount) / (double) pt.TotalCount, clrYellowBar);
                            addBar(0, pt.TimeoutCount / (double) pt.TotalCount, clrRedBar);
                        }
                        else
                        {
                            if (pt.TotalCount - pt.ErrorCount - pt.TimeoutCount == 0)
                            {
                                addBar(0, max, clrFuchsiaBar);
                            }
                            else
                            {
                                if (percentiles.Contains("99"))
                                    addBar(0, pt.MsResponsePrc99, clrBlueDarkBar);
                                if (percentiles.Contains("95"))
                                    addBar(0, pt.MsResponsePrc95, clrBlueBar);
                                if (percentiles.Contains("75"))
                                    addBar(0, pt.MsResponsePrc75, clrRedBar);
                                if (percentiles.Contains("50"))
                                    addBar(0, pt.MsResponsePrc50, clrYellowBar);
                                if (percentiles.Contains("25"))
                                    addBar(0, pt.MsResponsePrc25, clrGreenDarkBar);
                                if (percentiles.Contains("01"))
                                    addBar(0, pt.MsResponsePrc01, clrGreenBar);
                            }
                        }
                        p++;
                    }

                    curX += barW + barGap;
                }
                curX = curX - barGap + margin;

                return HttpResponse.Create(Ut.NewArray(
                    $"<svg width='{curX}' height='{margin + barH + margin}' style='border: 1px solid #999; background: #000;' xmlns='http://www.w3.org/2000/svg'><g>",
                    sb.ToString(),
                    "</g></svg>"
                ), "image/svg+xml");
            }
        }
    }

    class HttpingSettings
    {
        public List<HttpingTargetSettings> Targets = new List<HttpingTargetSettings> { new HttpingTargetSettings() };
    }

    class HttpingTargetSettings
    {
        public string Name = "Google"; // displayed
        public string InternalName = "Google"; // stored in db
        public string Url = "https://www.google.com";
        public TimeSpan Interval = TimeSpan.FromSeconds(5);
        public string MustContain = "";
        public string TimeZone = "GMT Standard Time";

        public override string ToString() => $"{Name} ({Url})";
    }

    class HttpingTarget
    {
        public HttpingTargetSettings Settings;

        public QueueViewable<HttpingPoint> Recent = new QueueViewable<HttpingPoint>(); // must hold a month's worth in order to compute monthly percentiles
        public QueueViewable<HttpingPointInterval> Twominutely = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Hourly = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Daily = new QueueViewable<HttpingPointInterval>();
        public QueueViewable<HttpingPointInterval> Monthly = new QueueViewable<HttpingPointInterval>();

        private long _siteId;
        private TimeZoneInfo _timezone;
        public object Lock = new object();
        private HttpingService _svc;

        public override string ToString() => $"{Settings.Name} ({Settings.Url}) : {Recent.Count:#,0} recent, {Twominutely.Count:#,0} twomin, {Hourly.Count:#,0} hourly, {Daily.Count:#,0} daily, {Monthly.Count:#,0} monthly";

        public void Start(HttpingService svc)
        {
            _svc = svc;
            _timezone = TimeZoneInfo.FindSystemTimeZoneById(Settings.TimeZone);

            using (var db = Db.Open())
            {
                _siteId = db.Query<TbHttpingSite>($"SELECT * FROM {nameof(TbHttpingSite)} WHERE {nameof(TbHttpingSite.InternalName)} = @name", new { name = Settings.InternalName }).SingleOrDefault()?.SiteId
                    ?? db.Insert(new TbHttpingSite { InternalName = Settings.InternalName });

                Recent = new QueueViewable<HttpingPoint>(db.Query<TbHttpingRecent>($@"
                        SELECT *
                        FROM {nameof(TbHttpingRecent)}
                        WHERE {nameof(TbHttpingRecent.SiteId)} = @siteId AND {nameof(TbHttpingRecent.Timestamp)} >= @limit
                        ORDER BY {nameof(TbHttpingRecent.Timestamp)}",
                        new { siteId = _siteId, limit = DateTime.UtcNow.AddDays(-35).ToDbDateTime() })
                    .Select(r => new HttpingPoint(r)));
                Twominutely = loadRecentIntervals(db, HttpingIntervalLength.TwoMinutes);
                Hourly = loadRecentIntervals(db, HttpingIntervalLength.Hour);
                Daily = loadRecentIntervals(db, HttpingIntervalLength.Day);
                Monthly = loadRecentIntervals(db, HttpingIntervalLength.Month);
            }

            new Thread(thread) { IsBackground = true }.Start();
        }

        private void recomputePercentiles()
        {
            using (var db = Db.Open())
            {
                Console.WriteLine($"Recomputing percentiles for site {_siteId}: loading data...");
                var allrecent = db.Query<TbHttpingRecent>($@"
                        SELECT *
                        FROM {nameof(TbHttpingRecent)}
                        WHERE {nameof(TbHttpingRecent.SiteId)} = @siteId
                        ORDER BY {nameof(TbHttpingRecent.Timestamp)}",
                        new { siteId = _siteId })
                    .Select(r => new HttpingPoint(r)).ToList();
                Console.WriteLine($"Loaded {allrecent.Count:#,0} datapoints. Comparing...");
                recomputePercentilesSingle(db, allrecent, GetStartOfTwominute, HttpingIntervalLength.TwoMinutes);
                recomputePercentilesSingle(db, allrecent, GetStartOfHour, HttpingIntervalLength.Hour);
                recomputePercentilesSingle(db, allrecent, GetStartOfLocalDayInUtc, HttpingIntervalLength.Day);
                recomputePercentilesSingle(db, allrecent, GetStartOfLocalMonthInUtc, HttpingIntervalLength.Month);
                Console.WriteLine($"Done.");
            }
        }

        private void recomputePercentilesSingle(SQLiteConnection db, IEnumerable<HttpingPoint> points, Func<DateTime, DateTime> getStart, HttpingIntervalLength length)
        {
            foreach (var grp in points.GroupBy(pt => getStart(pt.Timestamp.FromUnixSeconds())).OrderBy(g => g.Key).Skip(1).SkipLast(1))
            {
                var interval = new HttpingPointInterval { StartUtc = grp.Key };
                var good = grp.Select(g => g.MsResponse).Where(ms => ms != 0 && ms != 65535).Order().ToList();
                if (good.Count > 0)
                    SetPercentiles(ref interval.MsResponse, good);
                foreach (var sample in grp)
                    interval.CountSample(sample.MsResponse);
                var existing = db.Query<TbHttpingInterval>("SELECT * FROM TbHttpingInterval WHERE SiteId = @siteId AND StartTimestamp = @start AND IntervalLength = @length", new { siteId = _siteId, start = grp.Key.ToDbDateTime(), length }).SingleOrDefault();
                if (existing == null)
                {
                    Console.WriteLine($"MISSING: {_siteId}, {length}, {interval}");
                    db.Insert(new TbHttpingInterval(_siteId, length, interval));
                }
                else if (new HttpingPointInterval(existing).ToString() != interval.ToString())
                {
                    if (existing.TotalCount <= interval.TotalCount)
                    {
                        Console.WriteLine($"DIFFERENT: {_siteId}, {length}, {interval.StartUtc}: existing {existing.TotalCount} vs recomputed {interval.TotalCount}");
                        db.Update<TbHttpingInterval>(new TbHttpingInterval(_siteId, length, interval));
                    }
                }
            }
        }

        private QueueViewable<HttpingPointInterval> loadRecentIntervals(SQLiteConnection db, HttpingIntervalLength length)
        {
            return new QueueViewable<HttpingPointInterval>(db.Query<TbHttpingInterval>($@"
                    SELECT *
                    FROM {nameof(TbHttpingInterval)}
                    WHERE {nameof(TbHttpingInterval.SiteId)} = @siteId AND {nameof(TbHttpingInterval.IntervalLength)} = @length
                    ORDER BY {nameof(TbHttpingInterval.StartTimestamp)} DESC
                    LIMIT 500",
                    new { siteId = _siteId, length })
                .Select(r => new HttpingPointInterval(r)).Reverse());
        }

        private void thread()
        {
            while (true)
            {
                var next = DateTime.UtcNow + Settings.Interval;
                try
                {
                    var hc = new HttpClient();
                    hc.Timeout = TimeSpan.FromSeconds(Settings.Interval.TotalSeconds * 0.90);

                    double msResponse = -1;
                    bool error = false;
                    bool ok = false;
                    var start = DateTime.UtcNow;
                    try
                    {
                        if (!_svc.IsGoodInternetConnection())
                            goto skip;
                        var response = hc.GetAsync(Settings.Url).GetAwaiter().GetResult();
                        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        msResponse = (DateTime.UtcNow - start).TotalMilliseconds;
                        if (response.StatusCode == System.Net.HttpStatusCode.OK && Encoding.UTF8.GetString(bytes).Contains(Settings.MustContain))
                            ok = true;
                        if (!_svc.IsGoodInternetConnection())
                            goto skip;
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
                        using (var db = Db.Open())
                            db.Insert(new TbHttpingRecent { SiteId = _siteId, Timestamp = start.ToDbDateTime(), MsResponse = pt.MsResponse });
                        // Maintain the last 35 days in order to calculate monthly percentiles precisely
                        var cutoff = DateTime.UtcNow.AddDays(-35).ToUnixSeconds();
                        while (Recent.Count > 0 && Recent[0].Timestamp < cutoff)
                            Recent.Dequeue();

                        // Recalculate stats if we've crossed into the next minute
                        var prevPt = Recent.Count >= 2 ? Recent[Recent.Count - 2].Timestamp.FromUnixSeconds() : (DateTime?) null;
                        if (prevPt != null && prevPt.Value.TruncatedToMinutes() != start.TruncatedToMinutes())
                        {
                            AddIntervalIfRequired(Twominutely, prevPt.Value, start, GetStartOfTwominute, HttpingIntervalLength.TwoMinutes);
                            AddIntervalIfRequired(Hourly, prevPt.Value, start, GetStartOfHour, HttpingIntervalLength.Hour);
                            AddIntervalIfRequired(Daily, prevPt.Value, start, GetStartOfLocalDayInUtc, HttpingIntervalLength.Day);
                            AddIntervalIfRequired(Monthly, prevPt.Value, start, GetStartOfLocalMonthInUtc, HttpingIntervalLength.Month);
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

                skip:
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

        private void AddIntervalIfRequired(QueueViewable<HttpingPointInterval> queue, DateTime dtPrevUtc, DateTime dtCurUtc, Func<DateTime, DateTime> getIntervalStart, HttpingIntervalLength length)
        {
            var startPrevUtc = getIntervalStart(dtPrevUtc);
            var startCurUtc = getIntervalStart(dtCurUtc);
            if (startPrevUtc != startCurUtc)
            {
                var stat = ComputeStat(startPrevUtc, startCurUtc);
                queue.Enqueue(stat);
                using (var db = Db.Open())
                    db.Insert(new TbHttpingInterval(_siteId, length, stat));
            }
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
                if (interval.CountSample(pt.MsResponse))
                    msResponse.Add(pt.MsResponse);
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

        public HttpingPoint(TbHttpingRecent r) : this()
        {
            Timestamp = (uint) (r.Timestamp / 1000);
            MsResponse = (ushort) r.MsResponse;
        }

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

        public HttpingPointInterval(TbHttpingInterval r) : this()
        {
            StartUtc = r.StartTimestamp.FromDbDateTime();
            TotalCount = r.TotalCount;
            TimeoutCount = r.TimeoutCount;
            ErrorCount = r.ErrorCount;
            MsResponse.Prc01 = (ushort) r.MsResponsePrc01;
            MsResponse.Prc25 = (ushort) r.MsResponsePrc25;
            MsResponse.Prc50 = (ushort) r.MsResponsePrc50;
            MsResponse.Prc75 = (ushort) r.MsResponsePrc75;
            MsResponse.Prc95 = (ushort) r.MsResponsePrc95;
            MsResponse.Prc99 = (ushort) r.MsResponsePrc99;
        }

        public override string ToString() => $"{StartUtc} : {TotalCount:#,0} samples, {TimeoutCount + ErrorCount:#,0} timeouts/errors, {MsResponse}";

        public bool CountSample(ushort msResponse)
        {
            TotalCount++;
            if (msResponse == 65535)
                TimeoutCount++;
            else if (msResponse == 0)
                ErrorCount++;
            else
                return true;
            return false;
        }
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

        public bool CountSample(ushort msResponse)
        {
            TotalCount++;
            if (msResponse == 65535)
                TimeoutCount++;
            else if (msResponse == 0)
                ErrorCount++;
            else
                return true;
            return false;
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

    enum HttpingIntervalLength
    {
        TwoMinutes = 1,
        Hour = 2,
        Day = 3,
        Month = 4, // calendar month, midnight 1st to next midnight 1st
    }

    class TbHttpingSite
    {
        [Key]
        public long SiteId { get; set; }
        public string InternalName { get; set; }
    }

    class TbHttpingRecent
    {
        public long SiteId { get; set; }
        public long Timestamp { get; set; }
        public int MsResponse { get; set; }
    }

    class TbHttpingInterval
    {
        [ExplicitKey]
        public long SiteId { get; set; }
        [ExplicitKey]
        public long StartTimestamp { get; set; }
        [ExplicitKey]
        public HttpingIntervalLength IntervalLength { get; set; }

        public int TotalCount { get; set; }
        public int TimeoutCount { get; set; }
        public int ErrorCount { get; set; }

        public int MsResponsePrc01 { get; set; } // timeouts and errors are not included
        public int MsResponsePrc25 { get; set; }
        public int MsResponsePrc50 { get; set; }
        public int MsResponsePrc75 { get; set; }
        public int MsResponsePrc95 { get; set; }
        public int MsResponsePrc99 { get; set; }

        public TbHttpingInterval() // for Dapper
        {
        }

        public TbHttpingInterval(long siteId, HttpingIntervalLength length, HttpingPointInterval stat)
        {
            SiteId = siteId;
            StartTimestamp = stat.StartUtc.ToDbDateTime();
            IntervalLength = length;

            TotalCount = stat.TotalCount;
            TimeoutCount = stat.TimeoutCount;
            ErrorCount = stat.ErrorCount;

            MsResponsePrc01 = stat.MsResponse.Prc01;
            MsResponsePrc25 = stat.MsResponse.Prc25;
            MsResponsePrc50 = stat.MsResponse.Prc50;
            MsResponsePrc75 = stat.MsResponse.Prc75;
            MsResponsePrc95 = stat.MsResponse.Prc95;
            MsResponsePrc99 = stat.MsResponse.Prc99;
        }
    }
}
