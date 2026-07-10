namespace Plaxtar.Designer;

// Locates the repo's `designs/` directory by walking up from the content root
// (stops at the folder containing `designs/` or the repo root marked by `.git`).
public static class DesignPaths
{
    public static string DesignsDir(string contentRoot)
    {
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "designs");
            if (Directory.Exists(candidate)) return candidate;
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return candidate; // repo root
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate a 'designs' directory above the content root.");
    }
}
