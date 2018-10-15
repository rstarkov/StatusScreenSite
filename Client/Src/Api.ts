import * as moment from 'moment';
import * as pako from 'pako';
import { App } from './App';
import { IServiceDto } from './Dto';
import { Service } from './Service';
import * as Util from './Util';

export class Api {
    private app: App;
    private socket: WebSocket | null;
    private url: string;
    private services: Map<string, Service> = new Map();
    private timeOffset: moment.Duration = moment.duration(0);
    private reconnectTimerId: number;

    constructor(app: App) {
        this.app = app;
        let l = window.location;
        this.url = (l.protocol === "https:" ? "wss://" : "ws://") + l.host + l.pathname.replace("/app.html", "") + "/api";
    }

    Start(): void {
        console.log("API: connecting...");
        if (this.socket != null) {
            console.log("API: killing old socket...");
            this.socket.onopen = () => { }
            this.socket.onerror = () => { };
            this.socket.onclose = () => { };
            this.socket.onmessage = () => { };
            this.socket.close();
        }
        this.resetReconnect(20000);
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
            this.resetReconnect(2000);
        };
        this.socket.onmessage = (evt) => {
            this.app.ShowDisconnected(false);
            this.resetReconnect(20000);
            // could this be any uglier?...
            var reader = new FileReader();
            reader.onload = (read) => {
                let data = (read.target as any).result; // ... I guess it could! https://stackoverflow.com/a/35790786/33080
                let msg: ApiMessage = JSON.parse(new TextDecoder("utf-8").decode(pako.inflateRaw(data)));
                this.SetTimeOffset(moment.duration(moment(msg.CurrentTimeUtc).utc().diff(moment.utc())));
                console.log(`API: ${msg.ServiceName}, ${data.byteLength} bytes`);
                var svc = this.services.get(msg.ServiceName);
                if (svc)
                    svc.Update(msg.Data);
            };
            reader.readAsArrayBuffer(evt.data);
        };
    }

    private resetReconnect(afterMs: number): void {
        if (this.reconnectTimerId)
            clearTimeout(this.reconnectTimerId);
        this.reconnectTimerId = setTimeout(() => this.Start(), afterMs);
    }

    RegisterService(service: Service): void {
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