import * as Util from './Util'
import { Api } from './Api'
import { IServiceDto } from './Dto'

export interface IService {
    readonly Name: string;
    $Container: Util.Html;
    Start(api: Api): void;
    HandleUpdate(dto: IServiceDto): void;
}