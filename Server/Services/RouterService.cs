using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Util;

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
            long prevRx = long.MaxValue;
            long prevTx = long.MaxValue;
            var prevTimestamp = default(DateTime);
            double avgRx = 0;
            double avgTx = 0;
            double avgRxFast = 0;
            double avgTxFast = 0;

            while (true)
            {
                try
                {
                    http.ReqReferer = Settings.BaseUrl + "/Main_TrafficMonitor_realtime.asp";
                    var resp = http.Post(Settings.BaseUrl + "/update.cgi",
                        new HArg("output", "netdev"),
                        new HArg("_http_id", "TIDe855a6487043d70a")).Expect(HttpStatusCode.OK);
                    var timestamp = DateTime.UtcNow;

                    var match = Regex.Match(resp.DataString, @"'INTERNET':{rx:0x(?<rx>.*?),tx:0x(?<tx>.*?)}");
                    if (!match.Success)
                        throw new Exception();

                    var rx = Convert.ToInt64(match.Groups["rx"].Value, 16);
                    var tx = Convert.ToInt64(match.Groups["tx"].Value, 16);
                    var rxDiff = rx - prevRx;
                    var txDiff = tx - prevTx;
                    var timeDiff = (timestamp - prevTimestamp).TotalSeconds;
                    bool skip = rxDiff < 0 || txDiff < 0;
                    prevRx = rx;
                    prevTx = tx;
                    prevTimestamp = timestamp;

                    if (!skip)
                    {
                        var rxRate = rxDiff / timeDiff;
                        var txRate = txDiff / timeDiff;
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

                        SaveSettings();
                        SendUpdate(dto);
                    }
                }
                catch
                {
                    Thread.Sleep(TimeSpan.FromSeconds(60));
                    try { login(http); }
                    catch { }
                }

                Thread.Sleep(TimeSpan.FromSeconds(Settings.QueryInterval));
            }
        }

    }

    class RouterSettings
    {
        public string BaseUrl = "http://192.168.1.1";
        public double QueryInterval = 1;
        public double AverageDecay = 0.90;
        public double AverageDecayFast = 0.50;
        public string LoginAuth = "base64(user:pass)";
    }

    class RouterDto : ITypescriptDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public int RxLast { get; set; }
        public int TxLast { get; set; }
        public int RxAverageRecent { get; set; }
        public int TxAverageRecent { get; set; }
    }
}
