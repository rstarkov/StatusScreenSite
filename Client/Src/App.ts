import * as $ from 'jquery';
import '../Css/app.less';
import { Api } from './Api';
import { Service } from './Service';
import { HttpingService } from './Services/HttpingService';
import { PingService } from './Services/PingService';
import { ReloadService } from './Services/ReloadService';
import { RouterService } from './Services/RouterService';
import { TimeService } from './Services/TimeService';
import { WeatherService } from './Services/WeatherService';
import * as Util from './Util';

export class App {
    private api: Api = new Api(this);
    private services: Service[] = [];
    private disconnectedTimerId: number;
    private $container: Util.Html;
    private $pages: Map<string, Util.Html> = new Map();

    Start(): void {
        this.api.Start();
        let $body = $('body');
        $body.html(`
        <div id=Container>
            <table class="Container Page1">
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
            <table class="Container Page2">
                <tr>
                    <td class="HttpingServiceContainer ServiceContainer"></td>
                </tr>
            </table>
        </div>`);
        this.$container = Util.$get($body, '#Container');

        this.services.push(new ReloadService());
        this.services.push(new WeatherService());
        this.services.push(new TimeService());
        this.services.push(new PingService());
        this.services.push(new HttpingService());
        this.services.push(new RouterService());

        this.$pages.set('1', Util.$get($body, '.Container.Page1'));
        this.$pages.set('2', Util.$get($body, '.Container.Page2'));
        this.SwitchToPage('1');

        $body.on('keypress', (x) => {
            if (x.altKey || x.ctrlKey || x.shiftKey)
                return;
            if (this.$pages.has(x.key))
                this.SwitchToPage(x.key);
        });

        for (let svc of this.services) {
            try {
                svc.Initialise(this.api, this.$container);
                this.api.RegisterService(svc);
            }
            catch (ex) {
                console.warn("Failed to start service due to exception: " + svc.Name + ", " + ex);
            }
        }
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
    }

    SwitchToPage = (page: string): void => {
        for (let pg of this.$pages.values())
            pg.hide();
        this.$pages.get(page).show();
    };
}

(function (): void {
    let app = new App();
    app.Start();
})();
