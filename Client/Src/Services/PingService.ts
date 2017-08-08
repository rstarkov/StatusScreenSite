import * as Util from '../Util'
import { IService } from '../Service'

interface PingDto {
    Last: number | null;
}

export class PingService implements IService {
    readonly Name: string = 'PingService';
    $Container: Util.Html;

    private $Last: Util.Html;

    Start(): void {
        let $html = $(`
            <div class=header>Ping</div>
            <div><span></span> ms</div>
        `);
        this.$Container.append($html);
        this.$Last = $html.find('span');
    }

    HandleUpdate(dto: PingDto) {
        this.$Last.text(dto.Last == null ? '∞' : dto.Last.toString());
    }
}