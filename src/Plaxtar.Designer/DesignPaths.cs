namespace Plaxtar.Designer;

// Locates the design-output directory. By default `designs/` at the repo root (found by
// walking up from the content root). A host can override the folder via
// PlaxtarDesignerOptions.DesignsPath — a relative path is resolved against the repo root
// (per-FE subfolders like "designs/audit", ".plaxtar-designs"), an absolute path is used
// verbatim. The directory is created on first write, so it need not exist yet.
public static class DesignPaths
{
    public static string DesignsDir(string contentRoot, string? configured = null)
    {
        var root = RepoRoot(contentRoot);
        if (string.IsNullOrWhiteSpace(configured)) return Path.Combine(root, "designs");
        return Path.IsPathRooted(configured) ? configured : Path.Combine(root, configured);
    }

    // The base a relative designs path resolves against: the nearest ancestor that already
    // has a `designs/` folder or a `.git` marker; otherwise the content root itself.
    private static string RepoRoot(string contentRoot)
    {
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "designs")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return contentRoot;
    }
}
