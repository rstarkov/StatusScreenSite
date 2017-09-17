import * as Util from './Util'
import { Api } from './Api'
import { IServiceDto } from './Dto'

export abstract class Service {
    abstract readonly Name: string;
    protected $Container: Util.Html;
    protected _api: Api;
    private _$justUpdated: Util.Html;

    protected abstract Start(): void;
    protected abstract HandleUpdate(dto: IServiceDto): void;

    Initialise(api: Api, $container: Util.Html): void {
        let $div = $('<div>').attr('id', this.Name).addClass('ServiceContainer').css('visibility', 'hidden');
        Util.$get($container, `.${this.Name}Container`).append($div);
        $div.append('<div class=JustUpdated>');
        this.$Container = $div;
        this._$justUpdated = Util.$get(this.$Container, '.JustUpdated');

        this._api = api;
        this.Start();
    }

    Update(dto: IServiceDto): void {
        this.HandleUpdate(dto);
        this.$Container.css('visibility', 'visible');
        this._$justUpdated.stop(true, true).fadeTo(1, 0.99).fadeTo(1000, 0.01);
    }
}