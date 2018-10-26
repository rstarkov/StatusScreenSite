using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite.Services
{
    class RouterService : ServiceBase<RouterSettings, RouterDto>
    {
        public override string ServiceName => "RouterService";

        public RouterService(Server server, RouterSettings serviceSettings)
            : base(server, serviceSettings)
        {
        }

        public override void Start()
        {
            new Thread(thread) { IsBackground = true }.Start();
        }

        private void login(HClient http)
        {
            http.Cookies = new CookieContainer();
            http.ReqReferer = Settings.BaseUrl + "/Main_Login.asp";
            http.Post(Settings.BaseUrl + "/login.cgi",
                new HArg("group_id", ""),
                new HArg("action_mode", ""),
                new HArg("action_script", ""),
                new HArg("action_wait", "5"),
                new HArg("current_page", "Main_Login.asp"),
                new HArg("next_page", "index.asp"),
                new HArg("login_authorization", Settings.LoginAuth)).Expect(HttpStatusCode.OK);
            http.Cookies.Add(new Uri(Settings.BaseUrl), new Cookie("asus_token", http.Cookies.GetCookies(new Uri(Settings.BaseUrl + "/login.cgi"))[0].Value));
            http.Cookies.Add(new Uri(Settings.BaseUrl), new Cookie("bw_rtab", "INTERNET"));
            http.Cookies.Add(new Uri(Settings.BaseUrl), new Cookie("traffic_warning_0", "2017.7:1"));
        }

        private void thread()
        {
            var http = new HClient();
            login(http);
            var ptPrev = Settings.History.LastOrDefault();
            double avgRx = 0;
            double avgTx = 0;
            double avgRxFast = 0;
            double avgTxFast = 0;
            var recentHistory = new Queue<(double txRate, double rxRate)>();

            while (true)
            {
                var pt = new RouterHistoryPoint();

                try
                {
                    http.ReqReferer = Settings.BaseUrl + "/Main_TrafficMonitor_realtime.asp";
                    var resp = http.Post(Settings.BaseUrl + "/update.cgi",
                        new HArg("output", "netdev"),
                        new HArg("_http_id", "TIDe855a6487043d70a")).Expect(HttpStatusCode.OK);
                    pt.Timestamp = DateTime.UtcNow;

                    var match = Regex.Match(resp.DataString, @"'INTERNET':{rx:0x(?<rx>.*?),tx:0x(?<tx>.*?)}");
                    if (!match.Success)
                        throw new Exception();

                    pt.TxTotal = Convert.ToInt64(match.Groups["tx"].Value, 16);
                    pt.RxTotal = Convert.ToInt64(match.Groups["rx"].Value, 16);
                }
                catch
                {
                    Thread.Sleep(TimeSpan.FromSeconds(60));
                    try { login(http); continue; }
                    catch { }
                }

                Settings.History.Enqueue(pt);
                while (Settings.History.Peek().Timestamp < DateTime.UtcNow.AddHours(-24))
                    Settings.History.Dequeue();

                if (ptPrev != null)
                {
                    while (pt.TxTotal < ptPrev.TxTotal)
                        pt.TxTotal += uint.MaxValue;
                    while (pt.RxTotal < ptPrev.RxTotal)
                        pt.RxTotal += uint.MaxValue;
                    var txDiff = pt.TxTotal - ptPrev.TxTotal;
                    var rxDiff = pt.RxTotal - ptPrev.RxTotal;
                    var timeDiff = (pt.Timestamp - ptPrev.Timestamp).TotalSeconds;

                    var rxRate = rxDiff / timeDiff;
                    var txRate = txDiff / timeDiff;

                    recentHistory.Enqueue((txRate: txRate, rxRate: rxRate));
                    while (recentHistory.Count > 24)
                        recentHistory.Dequeue();

                    avgRx = avgRx * Settings.AverageDecay + rxRate * (1 - Settings.AverageDecay);
                    avgTx = avgTx * Settings.AverageDecay + txRate * (1 - Settings.AverageDecay);
                    avgRxFast = avgRxFast * Settings.AverageDecayFast + rxRate * (1 - Settings.AverageDecayFast);
                    avgTxFast = avgTxFast * Settings.AverageDecayFast + txRate * (1 - Settings.AverageDecayFast);
                    if (Math.Min(avgRx, avgRxFast) / Math.Max(avgRx, avgRxFast) < 0.5)
                        avgRx = avgRxFast = rxRate;
                    if (Math.Min(avgTx, avgTxFast) / Math.Max(avgTx, avgTxFast) < 0.5)
                        avgTx = avgTxFast = txRate;

                    var dto = new RouterDto();
                    dto.ValidUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(10);

                    dto.RxLast = (int) Math.Round(rxRate);
                    dto.TxLast = (int) Math.Round(txRate);
                    dto.RxAverageRecent = (int) Math.Round(avgRx);
                    dto.TxAverageRecent = (int) Math.Round(avgTx);
                    dto.HistoryRecent = recentHistory.Select(h => new RouterHistoryPointDto { TxRate = h.txRate, RxRate = h.rxRate }).ToArray();
                    dto.HistoryHourly = Enumerable.Range(1, 24).Select(h =>
                    {
                        var from = new DateTime(pt.Timestamp.Year, pt.Timestamp.Month, pt.Timestamp.Day, pt.Timestamp.Hour, 0, 0, DateTimeKind.Utc).AddHours(-24 + h);
                        var to = from.AddHours(1);
                        var earliest = Settings.History.Where(p => p.Timestamp >= from && p.Timestamp < to).MinElementOrDefault(p => p.Timestamp);
                        var latest = Settings.History.Where(p => p.Timestamp >= from && p.Timestamp < to).MaxElementOrDefault(p => p.Timestamp);
                        RouterHistoryPointDto result = null;
                        if (earliest != null && earliest.Timestamp != latest.Timestamp)
                            result = new RouterHistoryPointDto
                            {
                                TxRate = (latest.TxTotal - earliest.TxTotal) / (latest.Timestamp - earliest.Timestamp).TotalSeconds,
                                RxRate = (latest.RxTotal - earliest.RxTotal) / (latest.Timestamp - earliest.Timestamp).TotalSeconds,
                            };
                        if (result == null || result.TxRate < 0 || result.RxRate < 0)
                            return null;
                        else
                            return result;
                    }).ToArray();

                    SendUpdate(dto);
                }

                SaveSettings();
                ptPrev = pt;

                Thread.Sleep(TimeSpan.FromSeconds(Settings.QueryInterval));
            }
        }

        public override bool MigrateSchema(SQLiteConnection db, int curVersion)
        {
            return false;
        }
    }

    class RouterSettings
    {
        public string BaseUrl = "http://192.168.1.1";
        public double QueryInterval = 1;
        public double AverageDecay = 0.90;
        public double AverageDecayFast = 0.50;
        public string LoginAuth = "base64(user:pass)";
        public Queue<RouterHistoryPoint> History = new Queue<RouterHistoryPoint>();
    }

    class RouterHistoryPoint
    {
        public DateTime Timestamp;
        public long TxTotal;
        public long RxTotal;
    }

    class RouterDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public int RxLast { get; set; }
        public int TxLast { get; set; }
        public int RxAverageRecent { get; set; }
        public int TxAverageRecent { get; set; }
        public RouterHistoryPointDto[] HistoryRecent { get; set; }
        public RouterHistoryPointDto[] HistoryHourly { get; set; }
    }

    class RouterHistoryPointDto
    {
        public double TxRate { get; set; }
        public double RxRate { get; set; }
    }
}
