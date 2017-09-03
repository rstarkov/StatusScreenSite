import * as Util from './Util'
import { Api } from './Api'

export interface IService {
    readonly Name: string;
    $Container: Util.Html;
    Start(api: Api): void;
    HandleUpdate(dto: any): void;
}