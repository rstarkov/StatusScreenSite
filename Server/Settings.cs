using RT.Servers;
using RT.Util;
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
}
