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

interface Datasets { prc01: Plottable.Dataset, prc50: Plottable.Dataset, prc75: Plottable.Dataset, prc95: Plottable.Dataset }

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
    private _data2min: Datasets;
    private _plotDaily: Plottable.Plot;
    private _dataDaily: Datasets;

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
        if (this._data2min) {
            this._data2min.prc01.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc01 }; })));
            this._data2min.prc50.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc50 }; })));
            this._data2min.prc75.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc75 }; })));
            this._data2min.prc95.data(_.toArray(_(dto.Twominutely).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc95 }; })));
        }
        if (this._dataDaily) {
            this._dataDaily.prc01.data(_.toArray(_(dto.Daily).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc01 }; })));
            this._dataDaily.prc50.data(_.toArray(_(dto.Daily).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc50 }; })));
            this._dataDaily.prc75.data(_.toArray(_(dto.Daily).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc75 }; })));
            this._dataDaily.prc95.data(_.toArray(_(dto.Daily).map((v, i) => { return { X: i, Total: v.TotalCount, Errs: v.ErrorCount + v.TimeoutCount, Ms: v.MsResponsePrc95 }; })));
        }
    }

    public Redraw(): void {
        if (this._plotRecent)
            this._plotRecent.redraw();
        if (this._plot2min)
            this._plot2min.redraw();
        if (this._plotDaily)
            this._plotDaily.redraw();
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
                .domain(["green", "blue", "red", "fuchsia"])
                .range(['#08b025', '#1985f3', '#ff0000', '#ff00ff']);
            this._plotRecent = new Plottable.Plots.Bar()
                .addDataset(this._dataRecent)
                .x(function (d) { return d.x; }, xScale)
                .y(function (d) { return (d.y == 0 || d.y == 65535) ? 2000 : d.y; }, yScale)
                .attr('fill', (d) => { return (d.y == 0 || d.y == 65535) ? "fuchsia" : d.y > this._redMsCutoff ? "red" : d.y > this._greenMsCutoff ? "blue" : "green"; }, colorScale)
                .renderTo(<any>d3.select(Util.get(this.$tdChartRecent, 'div.plot')));
        }

        // Two min & daily chart
        [this._data2min, this._plot2min] = this.initStackedPlot(Util.get(this.$tdChart2min, 'div.plot'), yScale);
        [this._dataDaily, this._plotDaily] = this.initStackedPlot(Util.get(this.$tdChartDaily, 'div.plot'), yScale);
    }

    private initStackedPlot(target: HTMLElement, yScale: Plottable.Scale<{}, number>): [Datasets, Plottable.Plot] {
        var datas = {
            prc01: new Plottable.Dataset([], { color: "green" }), prc50: new Plottable.Dataset([], { color: "yellow" }),
            prc75: new Plottable.Dataset([], { color: "blue" }), prc95: new Plottable.Dataset([], { color: "red" }),
        };
        let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(30);
        let colorScale = new Plottable.Scales.Color()
            .domain(["green", "yellow", "blue", "red", "fuchsia", "grey"])
            .range(['#08b025', '#ffff00', '#1985f3', '#ff0000', '#ff00ff', '#404040']);
        var plot = new Plottable.Plots.Bar()
            .addDataset(datas.prc95)
            .addDataset(datas.prc75)
            .addDataset(datas.prc50)
            .addDataset(datas.prc01)
            .x((d) => { return d.X; }, xScale)
            .y((d) => { return d.Total > 0 ? d.Ms : 2000; }, yScale)
            .attr('fill', (d, i, dataset) => { return d.Total > 0 ? dataset.metadata().color : d.Errs > 0 ? "fuchsia" : "grey"; }, colorScale)
            .renderTo(<any>d3.select(target));
        return [datas, plot];
    }
}