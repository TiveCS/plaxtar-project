using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plaxtar.Designer;

// Serializable schema `plaxtar.designer/v1` (SPEC §7). Kept separate from the
// runtime DesignNode: on disk a node references a component by *name*, not Type.
public sealed class ScreenDoc
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "plaxtar.designer/v1";
    [JsonPropertyName("screen")] public string Screen { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "default";
    [JsonPropertyName("fe")] public string? Fe { get; set; }
    [JsonPropertyName("shell")] public string? Shell { get; set; }
    [JsonPropertyName("root")] public NodeDto Root { get; set; } = new();
}

public sealed class NodeDto
{
    [JsonPropertyName("component")] public string? Component { get; set; }
    [JsonPropertyName("element")] public string? Element { get; set; }
    [JsonPropertyName("src")] public string? Src { get; set; }
    [JsonPropertyName("class")] public string? CssClass { get; set; }
    [JsonPropertyName("style")] public string? Style { get; set; }
    [JsonPropertyName("params")] public Dictionary<string, JsonElement> Params { get; set; } = new();
    [JsonPropertyName("children")] public List<NodeDto> Children { get; set; } = new();
    [JsonPropertyName("slots")] public Dictionary<string, List<NodeDto>>? Slots { get; set; }
}
