namespace Plaxtar.Designer;

// Minimal in-memory Design Tree node for the #1 render spike.
// (The JSON-serializable schema lands in slice #2.)
public sealed record DesignNode(Type Type, Dictionary<string, object?> Params, List<DesignNode> Children)
{
    public static DesignNode Of(Type type, Dictionary<string, object?>? paramz = null, params DesignNode[] children)
        => new(type, paramz ?? new(), children.ToList());
}
