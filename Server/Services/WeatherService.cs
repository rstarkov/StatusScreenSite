using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite.Services
{
    class WeatherService : ServiceBase<WeatherSettings, WeatherDto>
    {
        public override string ServiceName => "WeatherService";

        public WeatherService(Server server, WeatherSettings serviceSettings)
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
                    var req = new HClient();
                    var result = req.Get("https://www.cl.cam.ac.uk/research/dtg/weather/current-obs.txt").Expect(HttpStatusCode.OK).DataString;
                    var datetime = Regex.Match(result, @"at (?<time>\d+:\d\d (AM|PM)) on (?<date>\d+ \w\w\w \d\d):");
                    var dt = DateTime.ParseExact(datetime.Groups["date"].Value + "@" + datetime.Groups["time"].Value, "dd MMM yy'@'h:mm tt", null);
                    var curTemp = decimal.Parse(Regex.Match(result, @"Temperature:\s+(?<temp>\d+(\.\d)?) C").Groups["temp"].Value);

                    Settings.Temperatures.RemoveAllByKey(date => date < DateTime.UtcNow - TimeSpan.FromHours(48));
                    Settings.Temperatures[DateTime.UtcNow] = curTemp;

                    var dto = new WeatherDto();
                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromMinutes(3);

                    dto.CurTemperature = Settings.Temperatures.Where(kvp => kvp.Key >= DateTime.UtcNow.AddMinutes(-15)).Average(kvp => kvp.Value);

                    var temps = Settings.Temperatures.OrderBy(kvp => kvp.Key).ToList();
                    var avg = temps.Select(kvp => (time: kvp.Key, temp: temps.Where(x => x.Key >= kvp.Key.AddMinutes(-7.5) && x.Key <= kvp.Key.AddMinutes(7.5)).Average(x => x.Value))).ToList();

                    var min = findExtreme(avg, seq => seq.MinElement(x => x.temp));
                    dto.MinTemperature = min.temp;
                    dto.MinTemperatureAtTime = $"{min.time.ToLocalTime():HH:mm}";
                    dto.MinTemperatureAtDay = min.time.ToLocalTime().Date == DateTime.Today ? "today" : "yesterday";

                    var max = findExtreme(avg, seq => seq.MaxElement(x => x.temp));
                    dto.MaxTemperature = max.temp;
                    dto.MaxTemperatureAtTime = $"{max.time.ToLocalTime():HH:mm}";
                    dto.MaxTemperatureAtDay = max.time.ToLocalTime().Date == DateTime.Today ? "today" : "yesterday";

                    SaveSettings();
                    SendUpdate(dto);
                }
                catch
                {
                }

                Thread.Sleep(TimeSpan.FromSeconds(60));
            }
        }

        private (DateTime time, decimal temp) findExtreme(List<(DateTime time, decimal temp)> seq, Func<IEnumerable<(DateTime time, decimal temp)>, (DateTime time, decimal temp)> getExtreme)
        {
            var today = DateTime.Today;
            var yesterday = DateTime.Today.AddDays(-1);
            var seqToday = seq.Where(s => s.time.ToLocalTime().Date == today);
            var seqYesterday = seq.Where(s => s.time.ToLocalTime().Date == yesterday);
            var result = getExtreme(seqToday);
            if (result.time > DateTime.UtcNow.AddHours(-2) && seqYesterday.Any())
                return getExtreme(seqYesterday);
            else
                return result;
        }
    }

    class WeatherSettings
    {
        public Dictionary<DateTime, decimal> Temperatures = new Dictionary<DateTime, decimal>();
    }

    class WeatherDto : ITypescriptDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public decimal CurTemperature;
        public decimal MinTemperature;
        public string MinTemperatureAtTime, MinTemperatureAtDay;
        public decimal MaxTemperature;
        public string MaxTemperatureAtTime, MaxTemperatureAtDay;
    }
}
