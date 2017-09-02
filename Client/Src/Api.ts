import { IService } from './Service'
import { App } from './App'
import * as moment from 'moment'

export class Api {
    private app: App;
    private socket: WebSocket | null;
    private url: string;
    private services: Map<string, IService> = new Map();

    constructor(app: App) {
        this.app = app;
        let l = window.location;
        this.url = (l.protocol === "https:" ? "wss://" : "ws://") + l.host + "/api";
    }

    Start(): void {
        if (this.socket != null)
            throw new Error("Trying to Start when there's a non-null socket.");
        this.socket = new WebSocket(this.url);
        this.socket.onerror = (evt) => {
        };
        this.socket.onclose = (evt) => {
            this.app.ShowDisconnected(true);
            this.socket = null;
            setTimeout(() => this.Start(), 2000);
        };
        this.socket.onmessage = (evt) => {
            this.app.ShowDisconnected(false);
            let msg: ApiMessage = JSON.parse(evt.data);
            this.app.SetTimeOffset(moment.duration(moment.utc().diff(moment(msg.CurrentTimeUtc).utc(true))));
            var svc = this.services.get(msg.ServiceName);
            if (svc) {
                svc.HandleUpdate(msg.Data);
                svc.$Container.css('visibility', 'visible');
                svc.$Container.find('.JustUpdated').stop(true, true).fadeTo(1, 0.99).fadeTo(1000, 0.01);
            }
        };
    }

    RegisterService(service: IService): void {
        this.services.set(service.Name, service);
    }
}

interface ApiMessage {
    ServiceName: string;
    CurrentTimeUtc: Date;
    Data: any;
}