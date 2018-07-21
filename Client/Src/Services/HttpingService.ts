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
                    <th>Recent chart</th>
                    <th>2 min chart</th>
                    <th>Daily chart</th>
                </tr></thead>
                <tbody></tbody>
            </table>
        `);
        this.$Container.append($html);
        this.$tbody = Util.$get($html, 'tbody');

        window.addEventListener("resize", () => {
            for (let tgt of this._entries.values()) {
                tgt.Redraw();
            }
        });
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
    private $tdChartRecent: Util.Html;
    private $tdChart2min: Util.Html;
    private $tdChartDaily: Util.Html;
    private _greenMsCutoff: number;
    private _redMsCutoff: number;
    private _plotRecent: Plottable.Plot;
    private _dataRecent: Plottable.Dataset;
    private _plot2min: Plottable.Plot;
    private _data2min: { prc01: Plottable.Dataset, prc50: Plottable.Dataset, prc75: Plottable.Dataset, prc95: Plottable.Dataset };
    private _plotDaily: Plottable.Plot;
    private _dataDaily: { prc01: Plottable.Dataset, prc50: Plottable.Dataset, prc75: Plottable.Dataset, prc95: Plottable.Dataset };

    public Add($tbody: Util.Html): void {
        this.$row = $('<tr>');
        this.$tdName = $('<td>');
        this.$td30m = $('<td>');
        this.$td24h = $('<td>');
        this.$td30d = $('<td>');
        this.$tdChartRecent = $('<td class="plot recent"><div class="plot recent"></div></td>');
        this.$tdChart2min = $('<td class="plot 2min"><div class="plot 2min"></div></td>');
        this.$tdChartDaily = $('<td class="plot daily"><div class="plot daily"></div></td>');
        this.$row.append(this.$tdName).append(this.$td30m).append(this.$td24h).append(this.$td30d)
            .append(this.$tdChartRecent).append(this.$tdChart2min).append(this.$tdChartDaily);
        $tbody.append(this.$row);
        this._dataRecent = null;
    }

    public Remove(): void {
        this.$row.remove();
    }

    public Update(dto: any): void {
        if (this._dataRecent == null)
            this.initialisePlots(dto);

        this.$tdName.text(dto.Name);
        this.$td30m.html(`${dto.Last30m.MsResponsePrc50} &nbsp; ${dto.Last30m.MsResponsePrc95}`);
        this.$td24h.html(`${dto.Last24h.MsResponsePrc50} &nbsp; ${dto.Last24h.MsResponsePrc95}`);
        this.$td30d.html(`${dto.Last30d.MsResponsePrc50} &nbsp; ${((dto.Last30d.ErrorCount + dto.Last30d.TimeoutCount) * 100 / dto.Last30d.TotalCount).toFixed(2)}%`);

        this._dataRecent.data(_.toArray(_(dto.Recent).map((v, i) => { return { x: i, y: v }; })));
        this._data2min.prc01.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { x: i, y: v.TotalCount == 0 ? 0 : v.MsResponsePrc01 }; })));
        this._data2min.prc50.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { x: i, y: v.TotalCount == 0 ? 0 : v.MsResponsePrc50 }; })));
        this._data2min.prc75.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { x: i, y: v.TotalCount == 0 ? 0 : v.MsResponsePrc75 }; })));
        this._data2min.prc95.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { x: i, y: v.TotalCount == 0 ? 0 : v.MsResponsePrc95 }; })));
    }

    public Redraw(): void {
        if (this._plotRecent)
            this._plotRecent.redraw();
        if (this._plot2min)
            this._plot2min.redraw();
    }

    private initialisePlots(dto: any): void {
        this._greenMsCutoff = dto.Last30d.MsResponsePrc75;
        if (this._greenMsCutoff > 5000)
            this._greenMsCutoff = 5000;
        this._redMsCutoff = this._greenMsCutoff * 1.5;
        let yScale = new Plottable.Scales.ModifiedLog(10).domainMin(dto.Last30d.MsResponsePrc01 * 0.9).domainMax(dto.Last30d.MsResponsePrc50 * 3);

        // Recent chart
        {
            this._dataRecent = new Plottable.Dataset();
            let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(30);
            let colorScale = new Plottable.Scales.Color()
                .domain(["1", "2", "3", "4"])
                .range(['#08b025', '#1985f3', '#ff0000', '#ff00ff']);
            this._plotRecent = new Plottable.Plots.Bar()
                .addDataset(this._dataRecent)
                .x(function (d) { return d.x; }, xScale)
                .y(function (d) { return (d.y == 0 || d.y == 65535) ? 2000 : d.y; }, yScale)
                .attr('fill', (d) => { return (d.y == 0 || d.y == 65535) ? "4" : d.y > this._redMsCutoff ? "3" : d.y > this._greenMsCutoff ? "2" : "1"; }, colorScale)
                .renderTo(<any>d3.select(Util.get(this.$tdChartRecent, 'div.plot')));
        }

        // Two min & daily chart
        {
            this._data2min = {
                prc01: new Plottable.Dataset(null, { color: "1" }), prc50: new Plottable.Dataset(null, { color: "2" }),
                prc75: new Plottable.Dataset(null, { color: "3" }), prc95: new Plottable.Dataset(null, { color: "4" }),
            };
            let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(30);
            let colorScale = new Plottable.Scales.Color()
                .domain(["1", "2", "3", "4", "5"])
                .range(['#08b025', '#ffff00', '#1985f3', '#ff0000', '#ff00ff']);
            this._plot2min = new Plottable.Plots.Bar()
                .addDataset(this._data2min.prc95)
                .addDataset(this._data2min.prc75)
                .addDataset(this._data2min.prc50)
                .addDataset(this._data2min.prc01)
                .x(function (d) { return d.x; }, xScale)
                .y(function (d) { return (d.y == 0 || d.y == 65535) ? 2000 : d.y; }, yScale)
                .attr('fill', function (d, i, dataset) { return (d.y == 0 || d.y == 65535) ? "5" : dataset.metadata().color; }, colorScale)
                .renderTo(<any>d3.select(Util.get(this.$tdChart2min, 'div.plot')));
        }
    }
}