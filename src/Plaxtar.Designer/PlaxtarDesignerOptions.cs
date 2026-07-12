using System.Reflection;

namespace Plaxtar.Designer;

// Host-supplied configuration for the Composer. The package is component-library
// agnostic: the host declares which assemblies form its component palette, an
// optional namespace filter (mirrors targeting .Base.UI / a module FE rather than
// app plumbing), and an optional default Shell (@layout) for new screens.
public sealed class PlaxtarDesignerOptions
{
    // Assemblies reflected for the component palette + name->Type resolution.
    public List<Assembly> ComponentAssemblies { get; } = new();

    // Only surface components whose namespace starts with this prefix (null = all).
    public string? NamespaceFilter { get; set; }

    // Layout component name used as the default Shell for new screens (null = none).
    // Resolved to a Type via the component assemblies at render time.
    public string? DefaultShellName { get; set; }

    // Micro-frontend identity written to the `fe` field of exported design trees
    // (e.g. "UI.Audit"). Lets the agent map a design back to its owning FE.
    public string? Fe { get; set; }

    // Output folder for design trees, the catalog manifest, and AGENTS.md. Null =
    // default `designs/` at the repo root. A relative path is resolved against the repo
    // root (so per-FE modules can scope their own folder, e.g. "designs/audit",
    // ".plaxtar-designs"); an absolute path is used as-is. Created on first write.
    public string? DesignsPath { get; set; }

    public PlaxtarDesignerOptions AddAssembly(Assembly assembly)
    {
        ComponentAssemblies.Add(assembly);
        return this;
    }

    public PlaxtarDesignerOptions AddAssemblyOf<T>() => AddAssembly(typeof(T).Assembly);
}
