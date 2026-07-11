namespace Plaxtar.Designer;

// Mutable design-tree node. One of three kinds: a **component** (Component set), a
// raw HTML **element** (Element = tag), or a **text** node (Text set — literal
// content). Params holds primitive/enum values for component nodes; structure lives
// in Children.
public sealed class EditableNode
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    public string? Component { get; set; }   // component node
    public string? Element { get; set; }     // raw HTML element node (e.g. "div")
    public string? Text { get; set; }        // text node: literal content
    public string? Src { get; set; }
    public string? CssClass { get; set; }    // element: class attribute
    public string? Style { get; set; }       // element: inline style (raw escape hatch)
    public Dictionary<string, string> Layout { get; set; } = new(); // structured CSS flow props -> node.layout
    public Dictionary<string, string> Attributes { get; set; } = new(); // element: passthrough attrs (data-*/aria-*/id...)
    public Dictionary<string, object?> Params { get; set; } = new();
    public Dictionary<string, string> Bindings { get; set; } = new();       // @bind-<Param> -> field name
    public Dictionary<string, string> Events { get; set; } = new();         // EventCallback <Name> -> handler name
    public List<EditableNode> Children { get; set; } = new();               // default ChildContent
    public Dictionary<string, List<EditableNode>> Slots { get; set; } = new(); // named RenderFragments

    public bool IsText => Text is not null;
    public bool IsElement => Element is not null;
    public bool IsComponent => Component is not null;
    public bool IsVoidElement => Element is not null && HtmlTags.Void.Contains(Element);

    public string DisplayName =>
        Text is not null ? $"“{(Text.Length > 18 ? Text[..18] + "…" : Text)}”"
        : Element is not null ? $"<{Element}>"
        : Component ?? "?";

    public IEnumerable<EditableNode> AllChildren => Children.Concat(Slots.Values.SelectMany(x => x));
}

// Curated + structural HTML tag knowledge for the element palette and rendering.
public static class HtmlTags
{
    // Void elements carry no children/text and self-close.
    public static readonly HashSet<string> Void =
        new(StringComparer.OrdinalIgnoreCase) { "img", "input", "br", "hr", "area", "base", "col", "embed", "link", "meta", "source", "track", "wbr" };

    // Curated palette groups (label -> tags). A free-text box covers anything else.
    public static readonly (string Group, string[] Tags)[] Palette =
    {
        ("Layout",   new[] { "div", "section" }),
        ("Text",     new[] { "p", "h1", "h2", "h3", "h4", "h5", "h6", "a" }),
        ("Inline",   new[] { "span", "i", "b", "strong", "em" }),
        ("Lists",    new[] { "ul", "ol", "li" }),
        ("Forms",    new[] { "form", "label", "input", "select", "option", "button", "textarea" }),
        ("Semantic", new[] { "nav", "header", "footer", "article", "aside" }),
        ("Media",    new[] { "img" }),
    };
}

// Complex-param value markers (schema §7.3): a field reference `{ "$bind": "model" }`
// or a raw C# expression escape hatch `{ "$raw": "new Opts{…}" }`. The designer only
// ever captures the name/expression, never runtime logic.
public sealed record BindExpr(string Field);
public sealed record RawExpr(string Code);
