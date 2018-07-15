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
        public string StaticPath = @"..\Static";

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
                sb.Append($"{pt.Timestamp},{pt.MsResponse};");
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
}
