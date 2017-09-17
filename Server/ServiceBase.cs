﻿namespace StatusScreenSite
{
    interface IService
    {
        string ServiceName { get; }
        void Start();
        IServiceDto LastUpdate { get; }
    }

    abstract class ServiceBase<TSettings, TDto> : IService
        where TDto : IServiceDto
    {
        public abstract string ServiceName { get; }
        private Server Server { get; set; }
        protected TSettings Settings { get; private set; }
        public IServiceDto LastUpdate { get; private set; }

        public abstract void Start();

        public ServiceBase(Server server, TSettings serviceSettings)
        {
            Server = server;
            Settings = serviceSettings;
        }

        protected void SendUpdate(TDto dto)
        {
            LastUpdate = dto;
            Server.SendUpdate(ServiceName, dto);
        }

        protected void SaveSettings()
        {
            Server.SaveSettings();
        }
    }
}
