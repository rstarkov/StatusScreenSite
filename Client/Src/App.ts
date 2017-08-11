import { Api } from 'Api'
import { IService } from 'Service'
import { ReloadService } from 'Services/ReloadService'
import { WeatherService } from 'Services/WeatherService'
import { PingService } from 'Services/PingService'
import * as Util from 'Util'

export class App {
    private api: Api = new Api(this);
    private services: IService[] = [];
    private $DisconnectedOverlay: Util.Html

    Start(): void {
        this.api.Start();
        let $body = $('body');
        $body.html('<div id=Container></div>');
        let $container = $body.find('#Container');

        this.services.push(new ReloadService());
        this.services.push(new WeatherService());
        this.services.push(new PingService());

        for (let svc of this.services) {
            let $div = $('<div>').attr('id', svc.Name).addClass('ServiceContainer').css('visibility', 'hidden');
            $container.append($div);
            svc.$Container = $div;
            svc.Start();
            this.api.RegisterService(svc);
        }

        this.$DisconnectedOverlay = $('<div id=DisconnectedOverlay><div></div></div>').css('visibility', 'hidden');
        $body.append(this.$DisconnectedOverlay);
    }

    ShowDisconnected(show: boolean): void {
        this.$DisconnectedOverlay.css('visibility', show ? 'visible' : 'hidden');
    }
}

export function main(): void {
    let app = new App();
    app.Start();
}