using System.Text.Json;
using System.Text.Json.Serialization;

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
    public Dictionary<string, EventBinding> Events { get; set; } = new();    // EventCallback <Name> -> { handler?, to? }
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

// A component event slot's authored value (schema §events). `Handler` is the codegen
// method-stub name; `To` is a Transition target (ADR 0008): a bare State name
// (`modal-open`) = sibling State of this Screen, or dotted `screen.state` = another
// Screen. Both optional but at least one is set for the binding to exist.
[JsonConverter(typeof(EventBindingConverter))]
public sealed record EventBinding(string? Handler = null, string? To = null)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Handler) && string.IsNullOrWhiteSpace(To);
}

// Reads BOTH the legacy bare-string form (`"OnClick": "Handler"`) and the normalized
// object form (`{ "handler": "...", "to": "..." }`), so old design files still load;
// always WRITES the object form (schema is object-only per ADR 0008).
public sealed class EventBindingConverter : JsonConverter<EventBinding>
{
    public override EventBinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new EventBinding(Handler: reader.GetString());
            case JsonTokenType.Null:
                return new EventBinding();
            case JsonTokenType.StartObject:
                string? handler = null, to = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var prop = reader.GetString();
                    reader.Read();
                    if (prop == "handler") handler = reader.GetString();
                    else if (prop == "to") to = reader.GetString();
                    else reader.Skip();
                }
                return new EventBinding(handler, to);
            default:
                throw new JsonException($"Invalid event binding token: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, EventBinding value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(value.Handler)) writer.WriteString("handler", value.Handler);
        if (!string.IsNullOrWhiteSpace(value.To)) writer.WriteString("to", value.To);
        writer.WriteEndObject();
    }
}
