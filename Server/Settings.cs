using RT.Servers;
using RT.Util;
using StatusScreenSite.Services;

namespace StatusScreenSite
{
    [Settings("StatusScreenSite", SettingsKind.Global)]
    class Settings : SettingsBase
    {
        public HttpServerOptions HttpOptions = new HttpServerOptions();
        public string StaticPath = @"..\Static";

        public WeatherSettings WeatherSettings = new WeatherSettings();
    }
}
