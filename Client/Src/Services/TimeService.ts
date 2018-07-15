import * as moment from 'moment';
import { Api } from '../Api';
import { ITimeDto } from '../Dto';
import { Service } from '../Service';
import * as Util from '../Util';

export class TimeService extends Service {
    readonly Name: string = 'TimeService';

    private $LocalTime: Util.Html;
    private $Timezones: Util.Html;
    private _lastUpdate: ITimeDto;

    protected Start(): void {
        let $html = $(`
            <div class=local><span class=local></span></div>
            <table class=timezones></ul>
        `);
        this.$Container.append($html);
        this.$LocalTime = Util.$get($html, 'span.local');
        this.$Timezones = Util.$get($html, 'table.timezones');

        setInterval(() => this.updateDisplay(), 200);
    }

    protected HandleUpdate(dto: ITimeDto) {
        this._lastUpdate = dto;
        this.updateDisplay();
    }

    private updateDisplay(): void {
        if (this._lastUpdate == null)
            return;
        this.$LocalTime.text(this.niceTime(this._lastUpdate.LocalOffsetHours));
        this.$Timezones.html('');
        for (let tz of this._lastUpdate.TimeZones) {
            this.$Timezones.append(
                $('<tr>')
                    .append($('<td class=name>').text(tz.DisplayName))
                    .append($('<td class=time>').text(this.niceTime(tz.OffsetHours)))
            );
        }
    }

    private niceTime(utcOffset: number): string {
        return this._api.GetTimeUtc().add(utcOffset, 'hours').format('HH:mm');
    }
}