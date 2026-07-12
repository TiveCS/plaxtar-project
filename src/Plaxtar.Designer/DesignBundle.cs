using System.IO.Compression;

namespace Plaxtar.Designer;

// Download/Import of designs (#35) so work survives an ephemeral dev-server container.
// Bundles the design trees + <screen>.flow.json sidecars (and any .png); never the
// regenerable _catalog.json / AGENTS.md. Import is merge (imported wins) and zip-slip
// safe — entries are written by basename only, so an upload can't escape the folder.
public static class DesignBundle
{
    private static readonly HashSet<string> Regenerable =
        new(StringComparer.OrdinalIgnoreCase) { "_catalog.json", "AGENTS.md" };

    private static bool Bundleable(string name) =>
        !Regenerable.Contains(name) &&
        (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

    // Zip the designs folder. `screen` (optional) limits to one screen's files
    // (`<screen>.<state>.json`, `<screen>.flow.json`, `<screen>.png`).
    public static byte[] Zip(string designsDir, string? screen = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (Directory.Exists(designsDir))
                foreach (var file in Directory.EnumerateFiles(designsDir))
                {
                    var name = Path.GetFileName(file);
                    if (!Bundleable(name)) continue;
                    if (screen is not null && !name.StartsWith(screen + ".", StringComparison.Ordinal)) continue;
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var es = entry.Open();
                    using var fs = File.OpenRead(file);
                    fs.CopyTo(es);
                }
        }
        return ms.ToArray();
    }

    public sealed record ImportResult(int Files, int Screens);

    // Extract an uploaded bundle (.zip) or a single design tree (.json) into designsDir.
    // Merge, imported wins; skips regenerable files; basename-only writes (zip-slip safe).
    public static async Task<ImportResult> ImportAsync(Stream upload, string fileName, string designsDir)
    {
        Directory.CreateDirectory(designsDir);
        var written = new List<string>();

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = new ZipArchive(upload, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;         // directory entry
                var name = Path.GetFileName(entry.Name);                // zip-slip guard
                if (!Bundleable(name)) continue;
                await using var es = entry.Open();
                await WriteAsync(es, Path.Combine(designsDir, name));
                written.Add(name);
            }
        }
        else if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(fileName);
            if (Bundleable(name))
            {
                await WriteAsync(upload, Path.Combine(designsDir, name));
                written.Add(name);
            }
        }

        var screens = written.Select(ScreenOf).Distinct(StringComparer.Ordinal).Count();
        return new ImportResult(written.Count, screens);
    }

    private static async Task WriteAsync(Stream src, string dest)
    {
        await using var fs = File.Create(dest);
        await src.CopyToAsync(fs);
    }

    // `login.default.json` / `login.flow.json` -> `login`.
    private static string ScreenOf(string fileName)
    {
        var n = Path.GetFileNameWithoutExtension(fileName);
        var dot = n.IndexOf('.');
        return dot >= 0 ? n[..dot] : n;
    }
}
