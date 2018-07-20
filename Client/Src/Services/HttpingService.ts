import * as d3 from 'd3';
import { entries } from 'd3';
import * as _ from 'lodash';
import * as Plottable from 'plottable';
import { IHttpingDto } from '../Dto';
import { Service } from '../Service';
import * as Util from '../Util';

export class HttpingService extends Service {
    readonly Name: string = 'HttpingService';

    private $tbody: Util.Html;
    private _entries: Map<string, Entry> = new Map();

    protected Start(): void {
        let $html = $(`
            <table class=HttpingSummary>
                <thead><tr>
                    <th>Site</th>
                    <th>30 min</th>
                    <th>24 hours</th>
                    <th>30 days</th>
                </tr></thead>
                <tbody></tbody>
            </table>
        `);
        this.$Container.append($html);
        this.$tbody = Util.$get($html, 'tbody');
    }

    protected HandleUpdate(dto: IHttpingDto) {
        for (let tgt of dto.Targets) {
            if (!this._entries.has(tgt.Name)) {
                this._entries.set(tgt.Name, new Entry());
                this._entries.get(tgt.Name).Add(this.$tbody);
            }
            this._entries.get(tgt.Name).Update(tgt);
        }
        // TODO: REMOVE
    }
}

class Entry {
    private $row: Util.Html;
    private $tdName: Util.Html;
    private $td30m: Util.Html;
    private $td24h: Util.Html;
    private $td30d: Util.Html;
    private $tdChart: Util.Html;

    public Add($tbody: Util.Html): void {
        this.$row = $('<tr>');
        this.$tdName = $('<td>');
        this.$td30m = $('<td>');
        this.$td24h = $('<td>');
        this.$td30d = $('<td>');
        //this.$tdChart = $('<td>');
        this.$row.append(this.$tdName).append(this.$td30m).append(this.$td24h).append(this.$td30d);//.append(this.$tdChart);
        $tbody.append(this.$row);
    }

    public Remove(): void {
        this.$row.remove();
    }

    public Update(dto: any): void {
        this.$tdName.text(dto.Name);
        this.$td30m.html(`${dto.Last30m.MsResponsePrc50} &nbsp; ${dto.Last30m.MsResponsePrc95}`);
        this.$td24h.html(`${dto.Last24h.MsResponsePrc50} &nbsp; ${dto.Last24h.MsResponsePrc95}`);
        this.$td30d.html(`${dto.Last30d.MsResponsePrc50} &nbsp; ${((dto.Last30d.ErrorCount + dto.Last30d.TimeoutCount) * 100 / dto.Last30d.TotalCount).toFixed(2)}%`);
        // todo: 2 min chart
    }
}