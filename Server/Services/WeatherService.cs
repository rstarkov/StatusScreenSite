using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using Dapper.Contrib.Extensions;
using Innovative.SolarCalculator;
using RT.Util;
using RT.Util.Drawing;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite.Services
{
    class WeatherService : ServiceBase<WeatherSettings, WeatherDto>
    {
        public override string ServiceName => "WeatherService";

        private Dictionary<DateTime, decimal> _temperatures = new Dictionary<DateTime, decimal>();

        public WeatherService(Server server, WeatherSettings serviceSettings)
            : base(server, serviceSettings)
        {
        }

        public override void Start()
        {
            using (var db = Db.Open())
                _temperatures = db.Query<TbWeatherTemperature>($@"SELECT * FROM {nameof(TbWeatherTemperature)} WHERE {nameof(TbWeatherTemperature.Timestamp)} > @limit", new { limit = DateTime.UtcNow.AddDays(-8).ToDbDateTime() })
                    .ToDictionary(r => r.Timestamp.FromDbDateTime(), r => (decimal) r.Temperature);

            new Thread(thread) { IsBackground = true }.Start();
        }

        private void thread()
        {
            while (true)
            {
                try
                {
                    var req = new HClient();
                    var result = req.Get("https://www.cl.cam.ac.uk/research/dtg/weather/current-obs.txt").Expect(HttpStatusCode.OK).DataString;
                    var datetime = Regex.Match(result, @"at (?<time>\d+:\d\d (AM|PM)) on (?<date>\d+ \w\w\w \d\d):");
                    var dt = DateTime.ParseExact(datetime.Groups["date"].Value + "@" + datetime.Groups["time"].Value, "dd MMM yy'@'h:mm tt", null);
                    var curTemp = decimal.Parse(Regex.Match(result, @"Temperature:\s+(?<temp>-?\d+(\.\d)?) C").Groups["temp"].Value);

                    _temperatures.RemoveAllByKey(date => date < DateTime.UtcNow - TimeSpan.FromDays(8));
                    _temperatures[DateTime.UtcNow] = curTemp;

                    using (var db = Db.Open())
                        db.Insert(new TbWeatherTemperature { Timestamp = DateTime.UtcNow.ToDbDateTime(), Temperature = (double) curTemp });

                    var dto = new WeatherDto();
                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromMinutes(30);

                    dto.CurTemperature = _temperatures.Where(kvp => kvp.Key >= DateTime.UtcNow.AddMinutes(-15)).Average(kvp => kvp.Value);

                    var temps = _temperatures.OrderBy(kvp => kvp.Key).ToList();
                    var avg = temps.Select(kvp => (time: kvp.Key, temp: temps.Where(x => x.Key >= kvp.Key.AddMinutes(-7.5) && x.Key <= kvp.Key.AddMinutes(7.5)).Average(x => x.Value))).ToList();

                    var min = findExtreme(avg, 5, seq => seq.MinElement(x => x.temp));
                    dto.MinTemperature = min.temp;
                    dto.MinTemperatureAtTime = $"{min.time.ToLocalTime():HH:mm}";
                    dto.MinTemperatureAtDay = min.time.ToLocalTime().Date == DateTime.Today ? "today" : "yesterday";

                    var max = findExtreme(avg, 12, seq => seq.MaxElement(x => x.temp));
                    dto.MaxTemperature = max.temp;
                    dto.MaxTemperatureAtTime = $"{max.time.ToLocalTime():HH:mm}";
                    dto.MaxTemperatureAtDay = max.time.ToLocalTime().Date == DateTime.Today ? "today" : "yesterday";

                    dto.CurTemperatureColor = getTemperatureDeviationColor(dto.CurTemperature, DateTime.Now, avg);
                    dto.MinTemperatureColor = getTemperatureDeviationColor(min.temp, min.time.ToLocalTime(), avg);
                    dto.MaxTemperatureColor = getTemperatureDeviationColor(max.temp, max.time.ToLocalTime(), avg);

                    PopulateSunriseSunset(dto, DateTime.Today);

                    SendUpdate(dto);
                }
                catch
                {
                }

                Thread.Sleep(TimeSpan.FromSeconds(60));
            }
        }

        private void PopulateSunriseSunset(WeatherDto dto, DateTime today)
        {
            var timesToday = new SolarTimes(DateTimeOffset.Now, Settings.Latitude, Settings.Longitude);
            var timesTomorrow = new SolarTimes(DateTimeOffset.Now.AddDays(1), Settings.Latitude, Settings.Longitude);
            dto.SunriseTime = $"{timesToday.Sunrise:HH:mm}";
            dto.SolarNoonTime = $"{timesToday.SolarNoon:HH:mm}";
            dto.SunsetTime = $"{timesToday.Sunset:HH:mm}";
            var sunsetDelta = timesTomorrow.Sunset.AddDays(-1) - timesToday.Sunset;
            dto.SunsetDeltaTime = (sunsetDelta >= TimeSpan.Zero ? "+" : "−") + $"{Math.Abs(sunsetDelta.TotalMinutes):0.0}m";
        }

        private (DateTime time, decimal temp) findExtreme(List<(DateTime time, decimal temp)> seq, int todayLimit, Func<IEnumerable<(DateTime time, decimal temp)>, (DateTime time, decimal temp)> getExtreme)
        {
            var today = DateTime.Today;
            var yesterday = DateTime.Today.AddDays(-1);
            var seqToday = seq.Where(s => s.time.ToLocalTime().Date == today).Reverse();
            var seqYesterday = seq.Where(s => s.time.ToLocalTime().Date == yesterday).Reverse();
            var result = getExtreme(seqToday);
            if ((result.time > DateTime.UtcNow.AddHours(-2) || DateTime.Now.Hour <= todayLimit) && seqYesterday.Any())
                return getExtreme(seqYesterday);
            else
                return result;
        }

        private static string getTemperatureDeviationColor(decimal temp, DateTime tempTime, List<(DateTime time, decimal temp)> avg)
        {
            var center = tempTime.ToLocalTime().AddDays(-1);
            var prevTempsAtSameTime = avg.Take(0).ToList(); // empty list of same type
            while (center.ToUniversalTime() > avg[0].time)
            {
                var from = center.AddHours(-0.5);
                var to = center.AddHours(0.5);
                var match = avg.Where(pt => pt.time >= from.ToUniversalTime() && pt.time <= to.ToUniversalTime()).MinElementOrDefault(pt => Math.Abs((pt.time - center.ToUniversalTime()).TotalSeconds));
                if (match.time != default(DateTime))
                    prevTempsAtSameTime.Add(match);
                center = center.AddDays(-1);
            }
            var color = Color.FromArgb(0xDF, 0x72, 0xFF); // purple = can't color by deviation
            if (prevTempsAtSameTime.Count >= 3)
            {
                var mean = (double) prevTempsAtSameTime.Average(pt => pt.temp);
                var stdev = Math.Sqrt(prevTempsAtSameTime.Sum(pt => ((double) pt.temp - mean) * ((double) pt.temp - mean)) / (prevTempsAtSameTime.Count - 1));
                var cur = (double) temp;
                var coldest = Color.FromArgb(0x2F, 0x9E, 0xFF);
                var warmest = Color.FromArgb(0xFF, 0x5D, 0x2F);
                if (cur < mean - stdev)
                    color = coldest;
                else if (cur > mean + stdev)
                    color = warmest;
                else
                    color = GraphicsUtil.ColorBlend(warmest, coldest, (cur - (mean - stdev)) / (2 * stdev));
            }
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public override bool MigrateSchema(SQLiteConnection db, int curVersion)
        {
            if (curVersion == 0)
            {
                db.Execute($@"CREATE TABLE {nameof(TbWeatherTemperature)} (
                    {nameof(TbWeatherTemperature.Timestamp)} INTEGER PRIMARY KEY,
                    {nameof(TbWeatherTemperature.Temperature)} REAL NOT NULL
                )");
                using (var trn = db.BeginTransaction())
                {
#pragma warning disable 612
                    foreach (var kvp in Settings.Temperatures)
                        db.Insert(new TbWeatherTemperature { Timestamp = kvp.Key.ToDbDateTime(), Temperature = (double) kvp.Value }, trn);
                    trn.Commit();
#pragma warning restore 612
                }
                return true;
            }

            return false;
        }
    }

    class WeatherSettings
    {
        public double Longitude = 0; // Longitude in degrees, east is positive
        public double Latitude = 0; // Latitude in degrees, north is positive

        [Obsolete]
        public Dictionary<DateTime, decimal> Temperatures = new Dictionary<DateTime, decimal>();
    }

    class WeatherDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public decimal CurTemperature { get; set; }
        public string CurTemperatureColor { get; set; }
        public decimal MinTemperature { get; set; }
        public string MinTemperatureColor { get; set; }
        public string MinTemperatureAtTime { get; set; }
        public string MinTemperatureAtDay { get; set; }
        public decimal MaxTemperature { get; set; }
        public string MaxTemperatureColor { get; set; }
        public string MaxTemperatureAtTime { get; set; }
        public string MaxTemperatureAtDay { get; set; }
        public string SunriseTime { get; set; }
        public string SolarNoonTime { get; set; }
        public string SunsetTime { get; set; }
        public string SunsetDeltaTime { get; set; }
    }

    class TbWeatherTemperature
    {
        [ExplicitKey]
        public long Timestamp { get; set; }
        public double Temperature { get; set; }
    }
}
