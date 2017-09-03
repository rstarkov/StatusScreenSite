import * as Util from '../Util'
import { IService } from '../Service'
import { IRouterDto } from '../Dto'
import 'plottable';
import * as d3 from 'd3';
import * as _ from 'lodash';

export class RouterService implements IService {
    readonly Name: string = 'RouterService';
    $Container: Util.Html;

    private $AvgRecentTx: Util.Html;
    private $AvgRecentRx: Util.Html;
    private $AvgHourlyTx: Util.Html;
    private $AvgHourlyRx: Util.Html;

    private _recentTx: Plottable.Dataset;
    private _recentRx: Plottable.Dataset;
    private _hourlyTx: Plottable.Dataset;
    private _hourlyRx: Plottable.Dataset;

    Start(): void {
        let $html = $(`
            <table>
                <tr class=headers>
                    <td class=kbs>KB/s</td>
                    <td>Recent traffic</td>
                    <td></td>
                    <td>Hourly traffic</td>
                    <td class=kbs>KB/s</td>
                </tr>
                <tr class="tx plots">
                    <td class=traffic-last><span class=tx-recent></span></td>
                    <td class=plot><div class=tx-recent></div></td>
                    <td class=spacer>Up</td>
                    <td class=plot><div class=tx-hourly></div></td>
                    <td class=traffic-last><span class=tx-hourly></span></td>
                </tr>
                <tr class="rx plots">
                    <td class=traffic-last><span class=rx-recent></span></td>
                    <td class=plot><div class=rx-recent></div></td>
                    <td class=spacer>Dn</td>
                    <td class=plot><div class=rx-hourly></div></td>
                    <td class=traffic-last><span class=rx-hourly></span></td>
                </tr>
            </table>
        `);
        this.$Container.append($html);

        this.$AvgRecentTx = Util.$get($html, 'span.tx-recent');
        this.$AvgRecentRx = Util.$get($html, 'span.rx-recent');
        this.$AvgHourlyTx = Util.$get($html, 'span.tx-hourly');
        this.$AvgHourlyRx = Util.$get($html, 'span.rx-hourly');

        this._recentTx = new Plottable.Dataset();
        this._recentRx = new Plottable.Dataset();
        this._hourlyTx = new Plottable.Dataset();
        this._hourlyRx = new Plottable.Dataset();

        this.createPlot(this._recentTx, Util.get(this.$Container, 'div.tx-recent'), true);
        this.createPlot(this._recentRx, Util.get(this.$Container, 'div.rx-recent'), false);
        this.createPlot(this._hourlyTx, Util.get(this.$Container, 'div.tx-hourly'), true);
        this.createPlot(this._hourlyRx, Util.get(this.$Container, 'div.rx-hourly'), false);
    }

    private createPlot(dataset: Plottable.Dataset, el: HTMLElement, isTx: boolean): void {
        let xScale = new Plottable.Scales.Linear().domainMin(-1).domainMax(24);
        let yMax = isTx ? 6100000 / 8 : 101000000 / 8;
        let yMin = yMax / 1000;
        let yScale = new Plottable.Scales.Linear().domainMax(Math.log10(yMax) - Math.log10(yMin)).domainMin(0);
        let colorScale = new Plottable.Scales.Color()
            .domain(["1", "2", "3", "4"])
            .range([isTx ? '#05305c' : '#573805', isTx ? '#0959aa' : '#966008', isTx ? '#1985f3' : '#ED980D', isTx ? '#64adf7' : '#f6bb5a', '#404040']);
        let bars = new Plottable.Plots.Bar()
            .addDataset(dataset)
            .x(function (d) { return d.x; }, xScale)
            .y(function (d) { return Math.log10(d.y == null ? yMax : d.y) - Math.log10(yMin); }, yScale)
            .attr('fill', function (d) { return d.y == null ? "5" : d.y < (yMax / 100) ? "1" : d.y < (yMax / 10) ? "2" : d.y < (yMax * 0.6) ? "3" : "4"; }, colorScale)
            .renderTo(<any>d3.select(el));

        window.addEventListener("resize", function () {
            bars.redraw();
        });
    }

    HandleUpdate(dto: IRouterDto) {
        this.$AvgRecentTx.text(this.niceRate(dto.TxAverageRecent));
        this.$AvgRecentRx.text(this.niceRate(dto.RxAverageRecent));
        this.$AvgHourlyTx.text(dto.HistoryHourly[23] == null ? "?" : this.niceRate(dto.HistoryHourly[23].TxRate));
        this.$AvgHourlyRx.text(dto.HistoryHourly[23] == null ? "?" : this.niceRate(dto.HistoryHourly[23].RxRate));

        this._recentTx.data(_.toArray(_(dto.HistoryRecent).map((v, i) => { return { x: 24 - dto.HistoryRecent.length + i, y: v == null ? null : v.TxRate }; })));
        this._recentRx.data(_.toArray(_(dto.HistoryRecent).map((v, i) => { return { x: 24 - dto.HistoryRecent.length + i, y: v == null ? null : v.RxRate }; })));

        this._hourlyTx.data(_.toArray(_(dto.HistoryHourly).map((v, i) => { return { x: i, y: v == null ? null : v.TxRate }; })));
        this._hourlyRx.data(_.toArray(_(dto.HistoryHourly).map((v, i) => { return { x: i, y: v == null ? null : v.RxRate }; })));
    }

    private niceRate(rate: number): string {
        return Math.round(rate / 1024).toLocaleString(undefined, { minimumFractionDigits: 0 });
    }
}