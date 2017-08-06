import * as Util from 'Util'
import { Api } from 'Api'

export interface IService {
    readonly Name: string;
    Start(container: Util.Html): void;
    HandleUpdate(dto: any): void;
}