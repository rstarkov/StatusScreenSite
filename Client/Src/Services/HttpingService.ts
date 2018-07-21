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
                <thead>
                    <tr>
                        <th rowspan=2>Site</th>
                        <th colspan=2 class=borderright>30 min</th>
                        <th colspan=2 class=borderright>24 hours</th>
                        <th colspan=3>30 days</th>
                        <th rowspan=2>Recent chart</th>
                        <th rowspan=2>2 min chart</th>
                        <th rowspan=2>Daily chart</th>
                    </tr>
                    <tr>
                        <th class=groupright>50%</th><th class="groupleft borderright">95%</th>
                        <th class=groupright>50%</th><th class="groupleft borderright">95%</th>
                        <th class=groupright>50%</th><th class="groupleft groupright">95%</th><th class=groupleft>Down</th>
                    </tr>
                </thead>
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

class Colors {
    static readonly GreenText = '#08b025';
    static readonly GreenBrightText = '#4ef459';
    static readonly BlueText = '#1985f3';
    static readonly RedText = '#ff0000';

    static readonly GreenBar = '#08b025';
    static readonly YellowBar = '#ffff00';
    static readonly BlueBar = '#1985f3';
    static readonly RedBar = '#ff0000';
    static readonly FuchsiaBar = '#ff00ff';
    static readonly GreyBar = '#404040';
}

class Entry {
    private $row: Util.Html;
    private $tdName: Util.Html;
    private $td30m50: Util.Html;
    private $td30m95: Util.Html;
    private $td24h50: Util.Html;
    private $td24h95: Util.Html;
    private $td30d50: Util.Html;
    private $td30d95: Util.Html;
    private $td30dErr: Util.Html;
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
        this.$row = $(`
            <tr>
                <td id=hpName></td>
                <td id=hp30m50 class=groupright></td>
                <td id=hp30m95 class="groupleft borderright"></td>
                <td id=hp24h50 class=groupright></td>
                <td id=hp24h95 class="groupleft borderright"></td>
                <td id=hp30d50 class=groupright></td>
                <td id=hp30d95 class="groupleft groupright"></td>
                <td id=hp30dErr class=groupleft></td>
                <td id=hpChartRecent class="plot recent"><div class="plot recent"></div></td>
                <td id=hpChart2min class="plot 2min"><div class="plot 2min"></div></td>
                <td id=hpChartDaily class="plot daily"><div class="plot daily"></div></td>
            </tr>
        `);
        $tbody.append(this.$row);
        this.$tdName = Util.$get(this.$row, 'td#hpName');
        this.$td30m50 = Util.$get(this.$row, 'td#hp30m50');
        this.$td30m95 = Util.$get(this.$row, 'td#hp30m95');
        this.$td24h50 = Util.$get(this.$row, 'td#hp24h50');
        this.$td24h95 = Util.$get(this.$row, 'td#hp24h95');
        this.$td30d50 = Util.$get(this.$row, 'td#hp30d50');
        this.$td30d95 = Util.$get(this.$row, 'td#hp30d95');
        this.$td30dErr = Util.$get(this.$row, 'td#hp30dErr');
        this.$tdChartRecent = Util.$get(this.$row, 'td#hpChartRecent');
        this.$tdChart2min = Util.$get(this.$row, 'td#hpChart2min');
        this.$tdChartDaily = Util.$get(this.$row, 'td#hpChartDaily');

        this._dataRecent = null;
    }

    public Remove(): void {
        this.$row.remove();
    }

    public Update(dto: any): void {
        if (this._dataRecent == null)
            this.initialisePlots(dto);

        this.$tdName.text(dto.Name);

        let set50prc = (tgt: Util.Html, time: number) => {
            tgt.html(`${time}`).css('color',
                time <= dto.Last30d.MsResponsePrc75 ? Colors.GreenText
                    : time >= dto.Last30d.MsResponsePrc75 * 1.5 ? Colors.RedText
                        : Colors.BlueText);
        }
        let set95prc = (tgt: Util.Html, time50prc: number, time95prc: number) => {
            tgt.html(`${time95prc}`).css('color',
                time95prc < time50prc * 1.1 ? Colors.GreenText
                    : time95prc < time50prc * 1.3 ? Colors.GreenBrightText
                        : time95prc > time50prc * 1.8 ? Colors.RedText
                            : Colors.BlueText);
        }
        set50prc(this.$td30m50, dto.Last30m.MsResponsePrc50);
        set95prc(this.$td30m95, dto.Last30m.MsResponsePrc50, dto.Last30m.MsResponsePrc95);
        set50prc(this.$td24h50, dto.Last24h.MsResponsePrc50);
        set95prc(this.$td24h95, dto.Last24h.MsResponsePrc50, dto.Last24h.MsResponsePrc95);
        this.$td30d50.html(`${dto.Last30d.MsResponsePrc50}`).css('color',
            dto.Last30d.MsResponsePrc50 < 100 ? Colors.GreenText
                : dto.Last30d.MsResponsePrc50 > 400 ? Colors.RedText
                    : Colors.BlueText);
        set95prc(this.$td30d95, dto.Last30d.MsResponsePrc50, dto.Last30d.MsResponsePrc95);
        let downtime30d = (dto.Last30d.ErrorCount + dto.Last30d.TimeoutCount) * 100 / dto.Last30d.TotalCount;
        this.$td30dErr.html(`${downtime30d.toFixed(2)}%`).css('color', downtime30d < 0.05 ? Colors.GreenText : downtime30d > 0.5 ? Colors.RedText : Colors.BlueText);

        this._dataRecent.data(dto.Recent);
        this._data2min.prc01.data(dto.Twominutely);
        this._data2min.prc50.data(dto.Twominutely);
        this._data2min.prc75.data(dto.Twominutely);
        this._data2min.prc95.data(dto.Twominutely);
        this._dataDaily.prc01.data(dto.Daily);
        this._dataDaily.prc50.data(dto.Daily);
        this._dataDaily.prc75.data(dto.Daily);
        this._dataDaily.prc95.data(dto.Daily);
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
                .range([Colors.GreenBar, Colors.BlueBar, Colors.RedBar, Colors.FuchsiaBar]);
            this._plotRecent = new Plottable.Plots.Bar()
                .addDataset(this._dataRecent)
                .x(function (pt, i) { return i; }, xScale)
                .y(function (pt, i) { return (pt == 0 || pt == 65535) ? 2000 : pt; }, yScale)
                .attr('fill', (pt) => { return (pt == 0 || pt == 65535) ? "fuchsia" : pt > this._redMsCutoff ? "red" : pt > this._greenMsCutoff ? "blue" : "green"; }, colorScale)
                .renderTo(<any>d3.select(Util.get(this.$tdChartRecent, 'div.plot')));
        }

        // Two min & daily chart
        [this._data2min, this._plot2min] = this.initStackedPlot(Util.get(this.$tdChart2min, 'div.plot'), yScale);
        [this._dataDaily, this._plotDaily] = this.initStackedPlot(Util.get(this.$tdChartDaily, 'div.plot'), yScale);
    }

    private initStackedPlot(target: HTMLElement, yScale: Plottable.Scale<{}, number>): [Datasets, Plottable.Plot] {
        var datas = {
            prc01: new Plottable.Dataset([], { type: "MsResponsePrc01", color: "green" }),
            prc50: new Plottable.Dataset([], { type: "MsResponsePrc50", color: "yellow" }),
            prc75: new Plottable.Dataset([], { type: "MsResponsePrc75", color: "blue" }),
            prc95: new Plottable.Dataset([], { type: "MsResponsePrc95", color: "red" }),
        };
        let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(30);
        let colorScale = new Plottable.Scales.Color()
            .domain(["green", "yellow", "blue", "red", "fuchsia", "grey"])
            .range([Colors.GreenBar, Colors.YellowBar, Colors.BlueBar, Colors.RedBar, Colors.FuchsiaBar, Colors.GreyBar]);
        var plot = new Plottable.Plots.Bar()
            .addDataset(datas.prc95)
            .addDataset(datas.prc75)
            .addDataset(datas.prc50)
            .addDataset(datas.prc01)
            .x((pt, i, ds) => { return i; }, xScale)
            .y((pt, i, ds) => { return pt.TotalCount > 0 ? pt[ds.metadata().type] : 2000; }, yScale)
            .attr('fill', (pt, i, ds) => { return pt.TotalCount > 0 ? ds.metadata().color : (pt.ErrorCount + pt.TimeoutCount) > 0 ? "fuchsia" : "grey"; }, colorScale)
            .renderTo(<any>d3.select(target));
        return [datas, plot];
    }
}