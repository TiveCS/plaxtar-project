namespace SampleHost.Designer;

// Best-effort component name -> source .razor path, by scanning the content root.
// (A real FE may prefer a build-time source manifest; this convention works in dev.)
public sealed class RazorSourceIndex
{
    private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    public RazorSourceIndex(string contentRoot)
    {
        foreach (var file in Directory.EnumerateFiles(contentRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            var name = Path.GetFileNameWithoutExtension(file);
            _map[name] = Path.GetRelativePath(contentRoot, file).Replace('\\', '/');
        }
    }

    public string? Find(string componentName) =>
        _map.TryGetValue(componentName, out var path) ? path : null;
}
