using System;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace StatusScreenSite
{
    interface IService
    {
        string ServiceName { get; }
        bool MigrateSchema(SQLiteConnection db, int curVersion);
        void Start();
        IServiceDto LastUpdate { get; }
        byte[] LastUpdateSerialized { get; }
    }

    abstract class ServiceBase<TSettings, TDto> : IService
        where TDto : IServiceDto
    {
        public abstract string ServiceName { get; }
        private Server Server { get; set; }
        protected TSettings Settings { get; private set; }
        public IServiceDto LastUpdate { get; private set; }
        public byte[] LastUpdateSerialized { get; private set; }

        public abstract bool MigrateSchema(SQLiteConnection db, int curVersion);
        public abstract void Start();

        public ServiceBase(Server server, TSettings serviceSettings)
        {
            Server = server;
            Settings = serviceSettings;
        }

        protected void SendUpdate(TDto dto)
        {
            LastUpdate = dto;
            using (var ms = new MemoryStream())
            {
                using (var gz = new DeflateStream(ms, CompressionLevel.Optimal, true))
                using (var wr = new StreamWriter(gz))
                    new JsonSerializer().Serialize(wr, new { ServiceName, CurrentTimeUtc = DateTime.UtcNow, Data = (object) dto });
                LastUpdateSerialized = ms.ToArray();
            }
            Server.SendUpdate(ServiceName, LastUpdateSerialized);
        }

        protected void SaveSettings()
        {
            Server.SaveSettings();
        }
    }
}
