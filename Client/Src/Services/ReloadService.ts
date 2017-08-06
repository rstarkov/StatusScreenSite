import * as Util from '../Util'
import { IService } from '../Service'

interface ReloadDto {
    StaticFilesHash: string;
}

export class ReloadService implements IService {
    readonly Name: string = 'ReloadService';
    LoadedHash: string | null = null;

    Start($container: Util.Html): void {
    }

    HandleUpdate(dto: ReloadDto) {
        if (this.LoadedHash == null) {
            this.LoadedHash = dto.StaticFilesHash;
            return;
        }
        if (this.LoadedHash != dto.StaticFilesHash) {
            location.reload(true);
        }
    }
}