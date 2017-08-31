﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RT.Servers;
using RT.TagSoup;
using RT.Util;
using RT.Util.ExtensionMethods;
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
            _services.Add(new RouterService(this, _settings.RouterSettings));

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
                    new LINK { rel = "stylesheet", type = "text/css", href = getStaticFileUrl("app.css") },
                    new LINK { rel = "stylesheet", type = "text/css", href = "https://cdnjs.cloudflare.com/ajax/libs/plottable.js/3.4.1/plottable.min.css" },
                    getScriptsFromManifest()
                ),
                new BODY(
                    new H1("Loading..."),
                    new SCRIPT { src = getStaticFileUrl("app.js") }
                )
            );
            return HttpResponse.Html(html);
        }

        private static string _manifestPath;
        private static DateTime _manifestLastWrite;
        private static List<SCRIPT> _manifestScripts;

        private IEnumerable<SCRIPT> getScriptsFromManifest()
        {
            if (_manifestPath == null)
                _manifestPath = PathUtil.AppPathCombine(_settings.StaticPath, "manifest.json");
            if (_manifestScripts == null || _manifestLastWrite != File.GetLastWriteTimeUtc(_manifestPath))
            {
                IEnumerable<KeyValuePair<string, JToken>> manifest = JObject.Parse(File.ReadAllText(_manifestPath));
                _manifestLastWrite = File.GetLastWriteTimeUtc(_manifestPath);
                _manifestScripts = manifest.Where(tkn => tkn.Key != "main.js" && tkn.Key != "main.css").Select(tkn => new SCRIPT { src = tkn.Value.Value<string>() }).ToList();
            }
            return _manifestScripts;
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
            return HttpResponse.File(path, mime, maxAge: 7 * 86400);
        }

        private HttpResponse handleApi(HttpRequest req)
        {
            var ws = new ApiWebSocket(this);
            _sockets.Add(ws);
            Console.WriteLine($"API socket {ws.Id}: connected from {req.ClientIPAddress}");
            Console.WriteLine($"   live sockets: {_sockets.Select(s => s.Id).Order().JoinString(", ")}");
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
            Console.WriteLine($"API socket {ws.Id}: disconnected");
            Console.WriteLine($"   live sockets: {_sockets.Select(s => s.Id).Order().JoinString(", ")}");
        }


        class ApiWebSocket : WebSocket
        {
            static int _nextId = 1;
            static object _lock = new object();

            private Server _server;

            public int Id { get; private set; }

            public ApiWebSocket(Server server)
            {
                _server = server;
                lock (_lock)
                {
                    Id = _nextId;
                    _nextId++;
                }
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
