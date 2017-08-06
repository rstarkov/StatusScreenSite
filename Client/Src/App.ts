import { Api } from 'Api'
import { IService } from 'Service'
import { ReloadService } from 'Services/ReloadService'
import { WeatherService } from 'Services/WeatherService'

class App {
    api: Api = new Api();
    services: IService[] = [];

    Start(): void {
        this.api.Start();
        let $body = $('body');
        $body.html('<div id=Container></div>');
        let $container = $body.find('#Container');

        this.services.push(new ReloadService());
        this.services.push(new WeatherService());

        for (let svc of this.services) {
            let $div = $('<div>').attr('id', svc.Name).addClass('ServiceContainer').css('visibility', 'hidden');
            svc.$Container = $div;
            svc.Start();
            $container.append($div);
            this.api.RegisterService(svc);
        }
    }
}

export function main(): void {
    let app = new App();
    app.Start();
}