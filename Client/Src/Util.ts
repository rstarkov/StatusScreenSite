export type Html = JQuery<HTMLElement>;

export function $get(obj: Html, content: any): Html {
    var result = obj.find(content).add(obj.filter(content));
    if (result.length != 1)
        throw new Error(result.length + ' match(es) for: ' + content);
    return result;
}

export function get(obj: Html, content: any): HTMLElement {
    return $get(obj, content)[0];
}
