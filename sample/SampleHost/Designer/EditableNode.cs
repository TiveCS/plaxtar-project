namespace SampleHost.Designer;

// Mutable design-tree node used while composing on the canvas.
// Params holds only primitive/enum CLR values (structure lives in Children);
// slots/bindings/events arrive in #7 / #13.
public sealed class EditableNode
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public required string Component { get; set; }
    public string? Src { get; set; }
    public Dictionary<string, object?> Params { get; set; } = new();
    public List<EditableNode> Children { get; set; } = new();
}
