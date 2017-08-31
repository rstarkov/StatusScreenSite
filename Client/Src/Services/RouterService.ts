import * as Util from '../Util'
import { IService } from '../Service'
import { IRouterDto } from '../Dto'

export class RouterService implements IService {
    readonly Name: string = 'RouterService';
    $Container: Util.Html;

    private $RxLast: Util.Html;
    private $TxLast: Util.Html;
    private $RxAverage: Util.Html;
    private $TxAverage: Util.Html;

    Start(): void {
        let $html = $(`
            <table>
                <tr> <td>RX:</td> <td class=rxAvg></td> <td class=rxLast></td> </tr>
                <tr> <td>TX:</td> <td class=txAvg></td> <td class=txLast></td> </tr>
            </table>
        `);
        this.$Container.append($html);
        this.$RxLast = $html.find('td.rxLast');
        this.$TxLast = $html.find('td.txLast');
        this.$RxAverage = $html.find('td.rxAvg');
        this.$TxAverage = $html.find('td.txAvg');
    }

    HandleUpdate(dto: IRouterDto) {
        //this.$RxLast.text(this.niceRate(dto.RxLast));
        //this.$TxLast.text(this.niceRate(dto.TxLast));
        this.$RxAverage.text(this.niceRate(dto.RxAverageRecent));
        this.$TxAverage.text(this.niceRate(dto.TxAverageRecent));
    }

    private niceRate(rate: number): string {
        return Math.round(rate / 1024).toLocaleString(undefined, { minimumFractionDigits: 0 }) + " KB/s";
    }
}