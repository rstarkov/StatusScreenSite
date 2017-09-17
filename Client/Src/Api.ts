import { IService } from './Service'
import { App } from './App'
import { IServiceDto } from './Dto'
import * as Util from './Util'
import * as moment from 'moment'

export class Api {
    private app: App;
    private socket: WebSocket | null;
    private url: string;
    private services: Map<string, IService> = new Map();
    private timeOffset: moment.Duration = moment.duration(0);

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
            this.SetTimeOffset(moment.duration(moment(msg.CurrentTimeUtc).utc().diff(moment.utc())));
            var svc = this.services.get(msg.ServiceName);
            if (svc)
                this.ServiceUpdate(svc, msg.Data);
        };
    }

    private ServiceUpdate(svc: IService, dto: IServiceDto): void {
        svc.HandleUpdate(dto);
        svc.$Container.css('visibility', 'visible');
        Util.$get(svc.$Container, '.JustUpdated').stop(true, true).fadeTo(1, 0.99).fadeTo(1000, 0.01);
    }

    RegisterService(service: IService): void {
        this.services.set(service.Name, service);
    }

    SetTimeOffset(offset: moment.Duration): void {
        this.timeOffset = offset;
    }

    GetTimeUtc(): moment.Moment {
        return moment.utc().add(this.timeOffset);
    }
}

interface ApiMessage {
    ServiceName: string;
    CurrentTimeUtc: Date;
    Data: IServiceDto;
}