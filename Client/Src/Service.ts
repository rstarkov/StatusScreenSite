import * as Util from './Util'
import { Api } from './Api'
import { IServiceDto } from './Dto'

export abstract class Service {
    abstract readonly Name: string;
    $Container: Util.Html;

    abstract Start(api: Api): void;
    abstract HandleUpdate(dto: IServiceDto): void;
}