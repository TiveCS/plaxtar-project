using System.Text.Json;

namespace Plaxtar.Designer;

// Read-only state-machine model for the Flow view (#32). Scans every State file of a
// Screen, enumerates each component node's EventCallback params as ports, and marks the
// ones that carry a Transition target (`to`). Box positions come from the editor-only
// `<screen>.flow.json` sidecar (plaxtar.flow/v1) or a simple auto-layout. Never codegen
// input — this is purely to visualise the graph.

// One event port on a component node: the event name, its Transition target (if any),
// and a `Path` locating the owning node inside its State file (so authoring can write
// `to` back to the exact node — node Ids are per-file and not on disk). Path grammar:
// "r" (root) or "o{i}" (overlay i), then "/c{i}" (Children[i]) or "/s{name}~{i}"
// (Slots[name][i]) segments.
public sealed record FlowPort(string Path, string Component, string Event, string? To)
{
    // Stable DOM key within a board (paths are per-file, so scope by state too).
    public string Key(string state) => $"{state}|{Path}|{Event}";
}

public sealed record FlowBox(string State, string FileBase, double X, double Y, IReadOnlyList<FlowPort> Ports);

public sealed record FlowGraphModel(string Screen, IReadOnlyList<FlowBox> Boxes)
{
    public bool HasState(string state) => Boxes.Any(b => b.State == state);
}

public static class FlowGraph
{
    // Auto-layout constants (also the render geometry — kept in one place).
    public const double BoxW = 240;
    public const double GapX = 80;
    public const double OriginX = 40, OriginY = 40;

    public static FlowGraphModel Build(string designsDir, string screen, ComponentCatalog catalog, ScreenStore store)
    {
        var states = store.List(designsDir)
            .Where(r => r.Screen == screen)
            // `default` first, then the rest alphabetically.
            .OrderBy(r => r.State == "default" ? 0 : 1).ThenBy(r => r.State, StringComparer.Ordinal)
            .ToList();

        var positions = LoadPositions(designsDir, screen);

        var boxes = new List<FlowBox>();
        for (int i = 0; i < states.Count; i++)
        {
            var r = states[i];
            var ports = ReadPorts(Path.Combine(designsDir, r.FileBase + ".json"), catalog);
            var (x, y) = positions.TryGetValue(r.State, out var p)
                ? (p.X, p.Y)
                : (OriginX + i * (BoxW + GapX), OriginY);
            boxes.Add(new FlowBox(r.State, r.FileBase, x, y, ports));
        }
        return new FlowGraphModel(screen, boxes);
    }

    private static IReadOnlyList<FlowPort> ReadPorts(string file, ComponentCatalog catalog)
    {
        if (!File.Exists(file)) return Array.Empty<FlowPort>();
        ScreenDoc? doc;
        try { doc = JsonSerializer.Deserialize<ScreenDoc>(File.ReadAllText(file)); }
        catch { return Array.Empty<FlowPort>(); }
        if (doc is null) return Array.Empty<FlowPort>();

        var ports = new List<FlowPort>();
        void Walk(NodeDto? n, string path)
        {
            if (n is null) return;
            if (n.Component is { } comp)
            {
                // Ports = the component's EventCallback params (from the Catalog), unioned
                // with any events actually set in the file (covers components not in the
                // Catalog). A port's `to` is read from the node's events map.
                var catalogEvents = catalog.Components.FirstOrDefault(c => c.Name == comp)?
                    .Params.Where(p => p.Kind == ParamKind.Event).Select(p => p.Name) ?? Enumerable.Empty<string>();
                var names = new List<string>();
                foreach (var e in catalogEvents) if (!names.Contains(e)) names.Add(e);
                if (n.Events is not null) foreach (var e in n.Events.Keys) if (!names.Contains(e)) names.Add(e);

                foreach (var ev in names)
                {
                    var to = n.Events is not null && n.Events.TryGetValue(ev, out var b) ? b.To : null;
                    ports.Add(new FlowPort(path, comp, ev, to));
                }
            }
            for (int i = 0; i < n.Children.Count; i++) Walk(n.Children[i], $"{path}/c{i}");
            if (n.Slots is not null)
                foreach (var (name, kids) in n.Slots)
                    for (int i = 0; i < kids.Count; i++) Walk(kids[i], $"{path}/s{name}~{i}");
        }
        Walk(doc.Root, "r");
        if (doc.Overlays is not null)
            for (int k = 0; k < doc.Overlays.Count; k++) Walk(doc.Overlays[k], $"o{k}");
        return ports;
    }

    private static readonly JsonSerializerOptions FlowJson = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions FlowWrite = new() { WriteIndented = true };

    private static Dictionary<string, FlowPos> LoadPositions(string designsDir, string screen)
    {
        var path = Path.Combine(designsDir, $"{screen}.flow.json");
        if (!File.Exists(path)) return new();
        try
        {
            var doc = JsonSerializer.Deserialize<FlowDoc>(File.ReadAllText(path), FlowJson);
            return doc?.Positions ?? new();
        }
        catch { return new(); }
    }

    // Persist one State box's position into the editor-only <screen>.flow.json sidecar
    // (merging with any existing positions). Never read by codegen.
    public static async Task WritePositionAsync(string designsDir, string screen, string state, double x, double y)
    {
        var path = Path.Combine(designsDir, $"{screen}.flow.json");
        FlowDoc doc = File.Exists(path)
            ? (JsonSerializer.Deserialize<FlowDoc>(await File.ReadAllTextAsync(path), FlowJson) ?? new())
            : new();
        doc.Positions[state] = new FlowPos(Math.Round(x), Math.Round(y));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(doc, FlowWrite));
    }

    // Resolve a FlowPort.Path against a State's editable tree (root + overlays) to the
    // owning node, so authoring can set its event target. Null if the path no longer resolves.
    public static EditableNode? Navigate(EditableNode root, IReadOnlyList<EditableNode> overlays, string path)
    {
        var slash = path.IndexOf('/');
        var head = slash < 0 ? path : path[..slash];
        EditableNode? node = head == "r" ? root
            : head.StartsWith('o') && int.TryParse(head[1..], out var oi) && oi < overlays.Count ? overlays[oi]
            : null;
        if (node is null || slash < 0) return node;
        foreach (var seg in path[(slash + 1)..].Split('/'))
        {
            if (seg.StartsWith('c') && int.TryParse(seg[1..], out var ci) && ci < node.Children.Count)
                node = node.Children[ci];
            else if (seg.StartsWith('s') && seg.LastIndexOf('~') is var t && t > 0
                     && int.TryParse(seg[(t + 1)..], out var si)
                     && node.Slots.TryGetValue(seg[1..t], out var kids) && si < kids.Count)
                node = kids[si];
            else return null;
        }
        return node;
    }
}

// Editor-only sidecar `<screen>.flow.json` (plaxtar.flow/v1): box positions only.
public sealed record FlowPos(
    [property: System.Text.Json.Serialization.JsonPropertyName("x")] double X,
    [property: System.Text.Json.Serialization.JsonPropertyName("y")] double Y);

public sealed class FlowDoc
{
    [System.Text.Json.Serialization.JsonPropertyName("schema")] public string Schema { get; set; } = "plaxtar.flow/v1";
    [System.Text.Json.Serialization.JsonPropertyName("positions")] public Dictionary<string, FlowPos> Positions { get; set; } = new();
}
