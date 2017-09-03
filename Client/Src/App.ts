import '../Css/app.less' // required to let webpack know that it needs to build this file
import { Api } from './Api'
import { IService } from './Service'
import { ReloadService } from './Services/ReloadService'
import { WeatherService } from './Services/WeatherService'
import { TimeService } from './Services/TimeService'
import { PingService } from './Services/PingService'
import { RouterService } from './Services/RouterService'
import * as Util from './Util'
import * as $ from 'jquery'

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
        this.services.push(new TimeService());
        this.services.push(new PingService());
        this.services.push(new RouterService());

        for (let svc of this.services) {
            let $div = $('<div>').attr('id', svc.Name).addClass('ServiceContainer').css('visibility', 'hidden');
            $container.append($div);
            $div.append('<div class=JustUpdated>');
            svc.$Container = $div;
            try {
                svc.Start(this.api);
                this.api.RegisterService(svc);
            }
            catch (ex) {
                console.warn("Failed to start service due to exception: " + svc.Name + ", " + ex);
            }
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
