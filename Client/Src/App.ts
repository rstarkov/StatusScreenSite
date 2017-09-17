import '../Css/app.less' // required to let webpack know that it needs to build this file
import { Api } from './Api'
import { Service } from './Service'
import { ReloadService } from './Services/ReloadService'
import { WeatherService } from './Services/WeatherService'
import { TimeService } from './Services/TimeService'
import { PingService } from './Services/PingService'
import { RouterService } from './Services/RouterService'
import * as Util from './Util'
import * as $ from 'jquery'

export class App {
    private api: Api = new Api(this);
    private services: Service[] = [];
    private $DisconnectedOverlay: Util.Html
    private disconnectedTimerId: number;
    private $container: Util.Html

    Start(): void {
        this.api.Start();
        let $body = $('body');
        $body.html(`
        <div id=Container>
            <table class=Container>
                <tr>
                    <td class="WeatherServiceContainer ServiceContainer"></td>
                    <td class="TimeServiceContainer ServiceContainer"></td>
                    <td class="PingServiceContainer ServiceContainer"></td>
                </tr>
                <tr>
                    <td class="RouterServiceContainer ServiceContainer" colspan=3></td>
                    <td class="ReloadServiceContainer ServiceContainer"></td>
                </tr>
            </table>
        </div>`);
        this.$container = Util.$get($body, '#Container');

        this.services.push(new ReloadService());
        this.services.push(new WeatherService());
        this.services.push(new TimeService());
        this.services.push(new PingService());
        this.services.push(new RouterService());

        for (let svc of this.services) {
            try {
                svc.Initialise(this.api, this.$container);
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
        if (show) {
            this.disconnectedTimerId = setTimeout(() => {
                this.$container.addClass('Disconnected');
            }, 5000);
        } else {
            if (this.disconnectedTimerId)
                clearTimeout(this.disconnectedTimerId);
            this.$container.removeClass('Disconnected');
        }

        if (show && this.$DisconnectedOverlay.css('visibility') != 'visible')
            this.$DisconnectedOverlay.css('visibility', 'visible').stop(true, true).fadeTo(1, 0.01).fadeTo(3000, 0.1).fadeTo(5000, 0.99);
        else if (!show)
            this.$DisconnectedOverlay.css('visibility', 'hidden');
    }
}

(function (): void {
    let app = new App();
    app.Start();
})();
