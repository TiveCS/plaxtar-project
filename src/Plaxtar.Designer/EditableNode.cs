namespace Plaxtar.Designer;

// Mutable design-tree node. A node is EITHER a component (Component set) OR a raw
// HTML element (Element = tag, with CssClass/Style). Params holds primitive/enum
// values for component nodes; structure lives in Children.
public sealed class EditableNode
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    public string? Component { get; set; }   // component node
    public string? Element { get; set; }     // raw HTML element node (e.g. "div")
    public string? Src { get; set; }
    public string? CssClass { get; set; }    // element: class attribute
    public string? Style { get; set; }       // element: inline style
    public Dictionary<string, object?> Params { get; set; } = new();
    public List<EditableNode> Children { get; set; } = new();               // default ChildContent
    public Dictionary<string, List<EditableNode>> Slots { get; set; } = new(); // named RenderFragments

    public bool IsElement => Element is not null;
    public string DisplayName => Element is not null ? $"<{Element}>" : Component ?? "?";
    public IEnumerable<EditableNode> AllChildren => Children.Concat(Slots.Values.SelectMany(x => x));
}
