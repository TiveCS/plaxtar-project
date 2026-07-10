using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace SampleHost.Designer;

// Resolves a component name (simple or full) to its Type by reflecting over the
// given assemblies. v1 precursor to the #4 Catalog; here it only needs name->Type.
public sealed class ComponentTypeResolver
{
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);

    public ComponentTypeResolver(IEnumerable<Assembly> assemblies)
    {
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

            foreach (var t in types)
            {
                if (t is null || t.IsAbstract || !typeof(IComponent).IsAssignableFrom(t)) continue;
                _byName[t.Name] = t;
                if (t.FullName is { } full) _byName[full] = t;
            }
        }
    }

    public Type Resolve(string name) =>
        _byName.TryGetValue(name, out var t)
            ? t
            : throw new InvalidOperationException($"Unknown component '{name}' — not found in loaded assemblies.");
}
