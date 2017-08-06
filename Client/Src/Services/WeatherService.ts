import * as Util from '../Util'
import { IService } from '../Service'

interface WeatherDto {
    CurTemperature: number;
    MinTemperature: number;
    MinTemperatureAt: string;
    MaxTemperature: number;
    MaxTemperatureAt: string;
}

export class WeatherService implements IService {
    readonly Name: string = 'WeatherService';
    $Container: Util.Html;

    private $CurTemp: Util.Html;
    private $MinTemp: Util.Html;
    private $MinTempAt: Util.Html;
    private $MaxTemp: Util.Html;
    private $MaxTempAt: Util.Html;

    Start(): void {
        let $html = $(`
            <div class=cur><span class=cur></span></div>
            <div class=min><span class=min></span> <span class=minAt></span></div>
             <div class=max><span class=max></span> <span class=maxAt></span></div>
        `);
        this.$Container.append($html);
        this.$CurTemp = $html.find('span.cur');
        this.$MinTemp = $html.find('span.min');
        this.$MinTempAt = $html.find('span.minAt');
        this.$MaxTemp = $html.find('span.max');
        this.$MaxTempAt = $html.find('span.maxAt');
    }

    HandleUpdate(dto: WeatherDto) {
        this.$CurTemp.text(this.niceTemp(dto.CurTemperature));
        this.$MinTemp.text(this.niceTemp(dto.MinTemperature));
        this.$MinTempAt.text(dto.MinTemperatureAt);
        this.$MaxTemp.text(this.niceTemp(dto.MaxTemperature));
        this.$MaxTempAt.text(dto.MaxTemperatureAt);
    }

    private niceTemp(temp: number): string {
        if (temp >= 0)
            return temp.toFixed(1) + " °C";
        else
            return "−" + (-temp).toFixed(1) + " °C";
    }
}