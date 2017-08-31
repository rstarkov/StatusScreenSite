import { Api } from './Api'
import { IService } from './Service'
import { ReloadService } from './Services/ReloadService'
import { WeatherService } from './Services/WeatherService'
import { PingService } from './Services/PingService'
import { RouterService } from './Services/RouterService'
import * as Util from './Util'
import * as $ from 'jquery'
import '../Css/app.less'

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
        this.services.push(new RouterService());

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

(function (): void {
    let app = new App();
    app.Start();
})();
