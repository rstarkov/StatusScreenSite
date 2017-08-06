import { IService } from 'Service'

export class Api {
    private socket: WebSocket;
    private url: string;
    private services: Map<string, IService> = new Map();

    constructor() {
        let l = window.location;
        this.url = (l.protocol === "https:" ? "wss://" : "ws://") + l.host + "/api";
    }

    Start(): void {
        this.socket = new WebSocket(this.url);
        this.socket.onerror = (evt) => {
        };
        this.socket.onclose = (evt) => {
        };
        this.socket.onmessage = (evt) => {
            let msg: ApiMessage = JSON.parse(evt.data);
            var svc = this.services.get(msg.ServiceName);
            if (svc) {
                svc.HandleUpdate(msg.Data);
                svc.$Container.css('visibility', 'visible');
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