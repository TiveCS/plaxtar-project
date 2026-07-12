namespace Plaxtar.Designer;

// Lists the screen files under designs/ (ignoring manifest/underscore files).
public sealed record ScreenRef(string Screen, string State, string FileBase);

public sealed class ScreenStore
{
    public IReadOnlyList<ScreenRef> List(string designsDir)
    {
        if (!Directory.Exists(designsDir)) return Array.Empty<ScreenRef>();

        return Directory.EnumerateFiles(designsDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            // Skip manifests (_catalog) and Flow-editor position sidecars (<screen>.flow.json),
            // which share the *.json glob but are not Screen states.
            .Where(name => name is not null && !name.StartsWith('_')
                           && !name.EndsWith(".flow", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                var dot = name!.LastIndexOf('.');
                var screen = dot >= 0 ? name[..dot] : name;
                var state = dot >= 0 ? name[(dot + 1)..] : "default";
                return new ScreenRef(screen, state, name);
            })
            .OrderBy(r => r.Screen).ThenBy(r => r.State)
            .ToList();
    }

    public void Delete(string designsDir, string fileBase)
    {
        var path = Path.Combine(designsDir, fileBase + ".json");
        if (File.Exists(path)) File.Delete(path);
    }
}
