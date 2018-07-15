import { IReloadDto } from '../Dto';
import { Service } from '../Service';
import * as Util from '../Util';

export class ReloadService extends Service {
    readonly Name: string = 'ReloadService';

    private LoadedHash: string | null = null;

    protected Start(): void {
    }

    protected HandleUpdate(dto: IReloadDto) {
        if (this.LoadedHash == null) {
            this.LoadedHash = dto.StaticFilesHash;
            return;
        }
        if (this.LoadedHash != dto.StaticFilesHash) {
            console.log("ReloadService: hash has changed; reloading page...");
            location.reload(true);
        }
    }
}