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
        console.log("API: connecting...");
        this.socket = new WebSocket(this.url);
        this.socket.onopen = (evt) => {
            console.log("API: connected.");
        };
        this.socket.onerror = (evt) => {
            console.log("API: socket error");
        };
        this.socket.onclose = (evt) => {
            console.log("API: socket closed; reconnecting shortly");
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