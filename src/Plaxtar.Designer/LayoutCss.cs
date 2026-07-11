namespace Plaxtar.Designer;

// Canonical mapping of a node's structured `layout` object (SPEC §5, schema §7) to
// real CSS. The canvas renders through this, and codegen must reproduce the same
// declarations (CLAUDE.md contract) so Preview matches the generated .razor.
public static class LayoutCss
{
    // layout key -> CSS property, emitted in this order for stable output.
    private static readonly (string Key, string Css)[] Map =
    {
        ("display", "display"),
        ("direction", "flex-direction"),
        ("wrap", "flex-wrap"),
        ("justify", "justify-content"),
        ("align", "align-items"),
        ("gap", "gap"),
        ("columns", "grid-template-columns"),
        ("rows", "grid-template-rows"),
        ("width", "width"),
        ("minWidth", "min-width"),
        ("maxWidth", "max-width"),
        ("padding", "padding"),
        ("margin", "margin"),
        ("position", "position"),
        ("top", "top"),
        ("right", "right"),
        ("bottom", "bottom"),
        ("left", "left"),
    };

    public static IReadOnlyList<string> Keys => Map.Select(m => m.Key).ToArray();

    // Build the inline-style string for a node's layout dict (empty -> null).
    public static string? ToStyle(IReadOnlyDictionary<string, string> layout)
    {
        if (layout.Count == 0) return null;
        var parts = Map
            .Where(m => layout.TryGetValue(m.Key, out var v) && !string.IsNullOrWhiteSpace(v))
            .Select(m => $"{m.Css}:{layout[m.Key]}");
        var css = string.Join(";", parts);
        return css.Length == 0 ? null : css;
    }

    // Combine structured layout with a node's raw-CSS escape hatch (raw wins on conflict).
    public static string? Combine(IReadOnlyDictionary<string, string> layout, string? rawStyle)
    {
        var fromLayout = ToStyle(layout);
        if (string.IsNullOrWhiteSpace(rawStyle)) return fromLayout;
        if (string.IsNullOrWhiteSpace(fromLayout)) return rawStyle;
        return $"{fromLayout};{rawStyle}";
    }
}
