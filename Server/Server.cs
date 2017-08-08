using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RT.Servers;
using RT.TagSoup;
using RT.Util;
using StatusScreenSite.Services;

namespace StatusScreenSite
{
    class Server
    {
        private Settings _settings;
        private bool _isRunning = false;
        private HashSet<ApiWebSocket> _sockets = new HashSet<ApiWebSocket>();
        private HashSet<IService> _services = new HashSet<IService>();

        public Server(Settings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_isRunning)
                throw new InvalidOperationException();
            _isRunning = true;

            var server = new HttpServer(_settings.HttpOptions);

            var resolver = new UrlResolver();
            resolver.Add(new UrlHook(path: "/", specificPath: true), handleIndex);
            resolver.Add(new UrlHook(path: "/app.html", specificPath: true), handleApp);
            resolver.Add(new UrlHook(path: "/api", specificPath: true), handleApi);
            resolver.Add(new UrlHook(path: null), handleStatic);

            server.Handler = resolver.Handle;
            server.StartListening(blocking: false);

            _services.Add(new ReloadService(this, _settings.StaticPath));
            _services.Add(new WeatherService(this, _settings.WeatherSettings));
            _services.Add(new PingService(this, _settings.PingSettings));

            foreach (var svc in _services)
                svc.Start();
        }

        public void SaveSettings()
        {
            _settings.SaveQuiet();
        }

        private HttpResponse handleIndex(HttpRequest req)
        {
            var html = new HTML(
                new HEAD(
                    new TITLE("Status Screen Site")
                ),
                new BODY { style = "margin:0; padding:0; overflow:hidden; background: #000;" }._(
                    new IFRAME { src = "/app.html", style = "display: block; border: none; width: 100vw; height: 100vh;" }
                )
            );
            return HttpResponse.Html(html);
        }

        private HttpResponse handleApp(HttpRequest req)
        {
            var html = new HTML(
                new HEAD(
                    new TITLE("Status Screen Site"),
                    new LINK { rel = "stylesheet", href = "https://fonts.googleapis.com/css?family=Open+Sans" },
                    new LINK { rel = "stylesheet", type = "text/css", href = getStaticFileUrl("app.css") },
                    new SCRIPT { src = "https://cdnjs.cloudflare.com/ajax/libs/systemjs/0.20.17/system-production.js" },
                    new SCRIPT { src = "https://cdnjs.cloudflare.com/ajax/libs/jquery/3.2.1/jquery.min.js" },
                    new SCRIPT { src = getStaticFileUrl("app.js") }
                ),
                new BODY(
                    new H1("Loading..."),
                    new SCRIPTLiteral(@"System.import('App').then(function(m) { m.main(); });")
                )
            );
            return HttpResponse.Html(html);
        }

        private string getStaticFileUrl(string path)
        {
            var info = new FileInfo(PathUtil.AppPathCombine(_settings.StaticPath, path));
            if (!info.Exists)
                throw new Exception($"Static file not found: {path}");
            return $"/{path}?v={info.LastWriteTimeUtc.ToFileTimeUtc()}-{info.Length}";
        }

        private HttpResponse handleStatic(HttpRequest req)
        {
            var path = req.Url.Path;
            if (path.Contains(".."))
                throw new HttpNotFoundException();
            var ext = Regex.Match(path, @"\.([\w\d]+)$");
            var mime = "application/octet-stream";
            if (ext.Success)
                mime = FileSystemOptions.GetDefaultMimeType(ext.Groups[1].Value);
            var staticRoot = Path.GetFullPath(PathUtil.AppPathCombine(_settings.StaticPath)) + @"\";
            path = path.Substring(1);
            path = Path.Combine(staticRoot, path);
            if (!path.StartsWith(staticRoot))
                throw new HttpNotFoundException();
            if (!File.Exists(path))
                throw new HttpNotFoundException();
            return HttpResponse.File(path, mime, maxAge: 7*86400);
        }

        private HttpResponse handleApi(HttpRequest req)
        {
            var ws = new ApiWebSocket(this);
            _sockets.Add(ws);
            return HttpResponse.WebSocket(ws);
        }

        public void SendUpdate(string serviceName, ITypescriptDto dto)
        {
            foreach (var ws in _sockets)
                sendUpdateToSocket(serviceName, dto, ws);
        }

        private void sendUpdateToSocket(string serviceName, ITypescriptDto dto, ApiWebSocket ws)
        {
            try
            {
                var msg = JsonConvert.SerializeObject(new { ServiceName = serviceName, CurrentTimeUtc = DateTime.UtcNow, Data = (object) dto });
                ws.SendMessage(msg);
            }
            catch { }
        }

        private void removeWebSocket(ApiWebSocket ws)
        {
            _sockets.Remove(ws);
        }


        class ApiWebSocket : WebSocket
        {
            private Server _server;

            public ApiWebSocket(Server server)
            {
                _server = server;
            }

            protected override void onBeginConnection()
            {
                foreach (var svc in _server._services)
                    if (svc.LastUpdate != null && svc.LastUpdate.ValidUntilUtc > DateTime.UtcNow)
                        _server.sendUpdateToSocket(svc.ServiceName, svc.LastUpdate, this);
            }

            protected override void onEndConnection()
            {
                _server.removeWebSocket(this);
            }
        }
    }
}
