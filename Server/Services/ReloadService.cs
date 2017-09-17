using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using RT.Util.ExtensionMethods;
using RT.Util.Paths;
using RT.Util.Streams;

namespace StatusScreenSite.Services
{
    class ReloadService : ServiceBase<string, ReloadDto>
    {
        public override string ServiceName => "ReloadService";
        private string StaticPath => Settings;

        public ReloadService(Server server, string staticPath)
            : base(server, staticPath)
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
                    // Hash metadata of everything in StaticPath
                    var hashStream = new HashingStream(new VoidStream(), MD5.Create()); // no need to dispose of it
                    var writer = new BinaryWriter(hashStream);
                    foreach (var path in new PathManager(StaticPath).GetFiles().OrderBy(f => f.FullName))
                    {
                        writer.Write(path.FullName);
                        writer.Write(path.Length);
                        writer.Write(path.LastWriteTimeUtc.ToBinary());
                    }
                    var hash = hashStream.Hash.ToHex();

                    SendUpdate(new ReloadDto { ValidUntilUtc = DateTime.UtcNow.AddYears(10), StaticFilesHash = hash });

                    // Sleep until we observe a change, but rescan fully every N minutes even we didn't see any changes
                    var minimumWait = DateTime.UtcNow.AddSeconds(2);
                    var watcher = new FileSystemWatcher();
                    watcher.IncludeSubdirectories = true;
                    watcher.Path = StaticPath;
                    watcher.WaitForChanged(WatcherChangeTypes.All, 20 * 60 * 1000);
                    // Enforce a minimum wait time in case the watcher breaks and starts firing endlessly for whatever reason
                    if (DateTime.UtcNow < minimumWait)
                        Thread.Sleep(minimumWait - DateTime.UtcNow);
                }
                catch
                {
                    Thread.Sleep(TimeSpan.FromSeconds(60));
                }
            }
        }
    }

    class ReloadDto : IServiceDto
    {
        public DateTime ValidUntilUtc { get; set; }
        public string StaticFilesHash { get; set; }
    }
}
