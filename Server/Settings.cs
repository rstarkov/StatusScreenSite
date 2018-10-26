using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Servers;
using RT.Util;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Serialization;
using StatusScreenSite.Services;

namespace StatusScreenSite
{
    [Settings("StatusScreenSite", SettingsKind.Global)]
    class Settings : SettingsBase
    {
        public HttpServerOptions HttpOptions = new HttpServerOptions();
        public string StaticPath = @"..\Static"; // may be absolute or relative to the executable location
        public string DbFilePath = "StatusScreenSite.db3"; // may be absolute or relative to the settings file location

        public WeatherSettings WeatherSettings = new WeatherSettings();
        public TimeSettings TimeSettings = new TimeSettings();
        public PingSettings PingSettings = new PingSettings();
        public HttpingSettings HttpingSettings = new HttpingSettings();
        public RouterSettings RouterSettings = new RouterSettings();
    }

    class DictionaryDateTimeDecimalSubstitutor : IClassifySubstitute<Dictionary<DateTime, decimal>, string>
    {
        public string ToSubstitute(Dictionary<DateTime, decimal> instance)
        {
            var sb = new StringBuilder();
            foreach (var pt in instance)
                sb.Append($"{pt.Key.ToBinary()},{pt.Value};");
            return sb.ToString();
        }

        public Dictionary<DateTime, decimal> FromSubstitute(string instance)
        {
            return instance.Split(';').Where(str => str != "")
                .Select(str =>
                {
                    var p = str.Split(',');
                    return new KeyValuePair<DateTime, decimal>(DateTime.FromBinary(long.Parse(p[0])), decimal.Parse(p[1]));
                }).ToDictionary();
        }
    }

    class QueueRouterHistoryPointSubstitutor : IClassifySubstitute<Queue<RouterHistoryPoint>, string>
    {
        public string ToSubstitute(Queue<RouterHistoryPoint> instance)
        {
            var sb = new StringBuilder();
            foreach (var pt in instance)
                sb.Append($"{pt.Timestamp.ToBinary()},{pt.TxTotal},{pt.RxTotal};");
            return sb.ToString();
        }

        public Queue<RouterHistoryPoint> FromSubstitute(string instance)
        {
            return new Queue<RouterHistoryPoint>(instance.Split(';').Where(str => str != "")
                .Select(str =>
                {
                    var p = str.Split(',');
                    return new RouterHistoryPoint { Timestamp = DateTime.FromBinary(long.Parse(p[0])), TxTotal = long.Parse(p[1]), RxTotal = long.Parse(p[2]) };
                }));
        }
    }

    class QueueHttpingPointSubstitutor : IClassifySubstitute<QueueViewable<HttpingPoint>, string>
    {
        public string ToSubstitute(QueueViewable<HttpingPoint> instance)
        {
            var sb = new StringBuilder();
            foreach (var pt in instance)
            {
                sb.Append(pt.Timestamp);
                sb.Append(',');
                sb.Append(pt.MsResponse);
                sb.Append(';');
            }
            return sb.ToString();
        }

        public QueueViewable<HttpingPoint> FromSubstitute(string instance)
        {
            return new QueueViewable<HttpingPoint>(instance.Split(';').Where(str => str != "")
                .Select(str =>
                {
                    var p = str.Split(',');
                    return new HttpingPoint { Timestamp = uint.Parse(p[0]), MsResponse = ushort.Parse(p[1]) };
                }));
        }
    }

    class QueueHttpingPointIntervalSubstitutor : IClassifySubstitute<QueueViewable<HttpingPointInterval>, string>
    {
        public string ToSubstitute(QueueViewable<HttpingPointInterval> instance)
        {
            var sb = new StringBuilder();
            foreach (var pt in instance)
                sb.Append($"{pt.StartUtc.ToBinary()},{pt.TotalCount},{pt.TimeoutCount},{pt.ErrorCount},{pt.MsResponse.Prc01},{pt.MsResponse.Prc25},{pt.MsResponse.Prc50},{pt.MsResponse.Prc75},{pt.MsResponse.Prc95},{pt.MsResponse.Prc99};");
            return sb.ToString();
        }

        public QueueViewable<HttpingPointInterval> FromSubstitute(string instance)
        {
            return new QueueViewable<HttpingPointInterval>(instance.Split(';').Where(str => str != "")
                .Select(str =>
                {
                    var p = str.Split(',');
                    return new HttpingPointInterval
                    {
                        StartUtc = DateTime.FromBinary(long.Parse(p[0])),
                        TotalCount = int.Parse(p[1]),
                        TimeoutCount = int.Parse(p[2]),
                        ErrorCount = int.Parse(p[3]),
                        MsResponse = new HttpingStatistic
                        {
                            Prc01 = ushort.Parse(p[4]),
                            Prc25 = ushort.Parse(p[5]),
                            Prc50 = ushort.Parse(p[6]),
                            Prc75 = ushort.Parse(p[7]),
                            Prc95 = ushort.Parse(p[8]),
                            Prc99 = ushort.Parse(p[9]),
                        },
                    };
                }));
        }
    }
}
