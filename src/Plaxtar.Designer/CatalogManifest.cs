using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plaxtar.Designer;

// Writes designs/_catalog.json — the manifest the AI agent reads (alongside design
// trees) to resolve param types and import paths during codegen.
public static class CatalogManifest
{
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
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, payload, Options);
        return path;
    }
}
