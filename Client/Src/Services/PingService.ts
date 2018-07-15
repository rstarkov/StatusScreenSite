import * as d3 from 'd3';
import * as _ from 'lodash';
import 'plottable';
import { IPingDto } from '../Dto';
import { Service } from '../Service';
import * as Util from '../Util';

export class PingService extends Service {
    readonly Name: string = 'PingService';

    private $Last: Util.Html;
    private _data: Plottable.Dataset;

    protected Start(): void {
        let $html = $(`
            <div class=pingNum><span></span> ms</div>
            <div class=header>Ping</div>
            <div class=pingPlot></div>
        `);
        this.$Container.append($html);
        this.$Last = Util.$get($html, 'span');

        this._data = new Plottable.Dataset();
        let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(24);
        let yScale = new Plottable.Scales.ModifiedLog(10).domainMin(10).domainMax(2000);
        let colorScale = new Plottable.Scales.Color()
            .domain(["1", "2", "3", "4"])
            .range(['#08b025', '#1985f3', '#ff0000', '#ff00ff']);
        let bars = new Plottable.Plots.Bar()
            .addDataset(this._data)
            .x(function (d) { return d.x; }, xScale)
            .y(function (d) { return d.y == null ? 2000 : d.y; }, yScale)
            .attr('fill', function (d) { return d.y == null ? "4" : d.y > 200 ? "3" : d.y > 40 ? "2" : "1"; }, colorScale)
            .renderTo(<any>d3.select(Util.get(this.$Container, '.pingPlot')));

        window.addEventListener("resize", function () {
            bars.redraw();
        });
    }

    protected HandleUpdate(dto: IPingDto) {
        this.$Last.text(dto.Last == null ? '∞' : dto.Last.toString());
        let values = _(dto.Recent).map((v, i) => { return { x: 24 - dto.Recent.length + i, y: v }; });
        this._data.data(_.toArray(values));
    }
}