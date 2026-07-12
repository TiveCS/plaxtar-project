using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plaxtar.Designer;

// Writes designs/_catalog.json — the manifest the AI agent reads (alongside design
// trees) to resolve param types and import paths during codegen. Also drops
// designs/AGENTS.md (the codegen guide) so the agent has the convention beside the trees.
public static class CatalogManifest
{
    private const string GuideResource = "Plaxtar.Designer.AgentGuide.md";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<string> WriteAsync(string designsDir, ComponentCatalog catalog)
    {
        var payload = new
        {
            schema = "plaxtar.designer/catalog-v1",
            components = catalog.Components,
        };

        Directory.CreateDirectory(designsDir);
        var path = Path.Combine(designsDir, "_catalog.json");
        await using (var fs = File.Create(path))
            await JsonSerializer.SerializeAsync(fs, payload, Options);

        await WriteAgentGuideAsync(designsDir);
        return path;
    }

    // Copy the embedded codegen guide to designs/AGENTS.md (read-only; regenerated on
    // every export, never hand-edited). The canonical source is docs/plaxtar-codegen.md.
    public static async Task<string?> WriteAgentGuideAsync(string designsDir)
    {
        await using var res = typeof(CatalogManifest).Assembly.GetManifestResourceStream(GuideResource);
        if (res is null) return null;
        Directory.CreateDirectory(designsDir);
        var path = Path.Combine(designsDir, "AGENTS.md");
        await using var fs = File.Create(path);
        await res.CopyToAsync(fs);
        return path;
    }
}
