import * as Util from '../Util'
import { IService } from '../Service'
import { IWeatherDto } from '../Dto'

export class WeatherService implements IService {
    readonly Name: string = 'WeatherService';
    $Container: Util.Html;

    private $CurTemp: Util.Html;
    private $MinTemp: Util.Html;
    private $MinTempAtTime: Util.Html;
    private $MinTempAtDay: Util.Html;
    private $MaxTemp: Util.Html;
    private $MaxTempAtTime: Util.Html;
    private $MaxTempAtDay: Util.Html;
    private $Sunrise: Util.Html;
    private $Sunset: Util.Html;
    private $SunsetDelta: Util.Html;

    Start(): void {
        let $html = $(`
            <div class=cur><span class=cur></span></div>
            <table>
                <tr class=min> <td class=temp></td> <td> at </td> <td class=atTime></td> <td class=atDay></td> </tr>
                <tr class=max> <td class=temp></td> <td> at </td> <td class=atTime></td> <td class=atDay></td> </tr>
            </table>
            <div class=solar><span class=sunrise>X</span> <span class=sunset></span> <span class=sunsetDelta></span></div>
        `);
        this.$Container.append($html);
        this.$CurTemp = $html.find('span.cur');
        this.$MinTemp = $html.find('tr.min td.temp');
        this.$MinTempAtTime = $html.find('tr.min td.atTime');
        this.$MinTempAtDay = $html.find('tr.min td.atDay');
        this.$MaxTemp = $html.find('tr.max td.temp');
        this.$MaxTempAtTime = $html.find('tr.max td.atTime');
        this.$MaxTempAtDay = $html.find('tr.max td.atDay');

        this.$Sunrise = $html.find('span.sunrise');
        this.$Sunset = $html.find('span.sunset');
        this.$SunsetDelta = $html.find('span.sunsetDelta');
    }

    HandleUpdate(dto: IWeatherDto) {
        this.$CurTemp.text(this.niceTemp(dto.CurTemperature));
        this.$MinTemp.text(this.niceTemp(dto.MinTemperature));
        this.$MinTempAtTime.text(dto.MinTemperatureAtTime);
        this.$MinTempAtDay.text(dto.MinTemperatureAtDay);
        this.$MaxTemp.text(this.niceTemp(dto.MaxTemperature));
        this.$MaxTempAtTime.text(dto.MaxTemperatureAtTime);
        this.$MaxTempAtDay.text(dto.MaxTemperatureAtDay);
        this.$Sunrise.text(dto.SunriseTime);
        this.$Sunset.text(dto.SunsetTime);
        this.$SunsetDelta.text(dto.SunsetDeltaTime);
    }

    private niceTemp(temp: number): string {
        if (temp >= 0)
            return temp.toFixed(1) + " °C";
        else
            return "−" + (-temp).toFixed(1) + " °C";
    }
}