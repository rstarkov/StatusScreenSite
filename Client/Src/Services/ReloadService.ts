import * as Util from '../Util'
import { IService } from '../Service'
import { IReloadDto } from '../Dto'

export class ReloadService implements IService {
    readonly Name: string = 'ReloadService';
    $Container: Util.Html;

    private LoadedHash: string | null = null;

    Start(): void {
    }

    HandleUpdate(dto: IReloadDto) {
        if (this.LoadedHash == null) {
            this.LoadedHash = dto.StaticFilesHash;
            return;
        }
        if (this.LoadedHash != dto.StaticFilesHash) {
            location.reload(true);
        }
    }
}