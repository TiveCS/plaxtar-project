using System.Text.Json;

namespace Plaxtar.Designer;

// Loads a `plaxtar.designer/v1` file and materializes it into a runtime DesignNode
// tree, coercing each JSON param value to the target [Parameter]'s CLR type.
public sealed class ScreenLoader
{
    private readonly ComponentTypeResolver _resolver;

    public ScreenLoader(ComponentTypeResolver resolver) => _resolver = resolver;

    public async Task<(ScreenDoc Doc, DesignNode Tree)> LoadAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<ScreenDoc>(fs)
                  ?? throw new InvalidOperationException($"Empty or invalid screen doc: {path}");
        return (doc, ToNode(doc.Root));
    }

    private DesignNode ToNode(NodeDto dto)
    {
        var type = _resolver.Resolve(dto.Component);

        var paramz = new Dictionary<string, object?>();
        foreach (var (key, element) in dto.Params)
        {
            var prop = type.GetProperty(key);
            if (prop is null) continue; // unknown param -> skip in v1
            paramz[key] = element.Deserialize(prop.PropertyType);
        }

        var children = dto.Children.Select(ToNode).ToList();
        return new DesignNode(type, paramz, children);
    }
}
