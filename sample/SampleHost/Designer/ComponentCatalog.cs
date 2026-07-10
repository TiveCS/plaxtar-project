using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace SampleHost.Designer;

// A parameter's editing kind (SPEC §6). Drives both the props panel and codegen.
public enum ParamKind { Primitive, Enum, ChildContent, Slot, TemplatedSlot, Event, Complex }

public sealed record ParamInfo(string Name, string Type, ParamKind Kind, string[]? EnumOptions);
public sealed record ComponentInfo(string Name, string Assembly, string? Src, IReadOnlyList<ParamInfo> Params);

// Reflects the given assemblies for component types and their [Parameter] metadata.
// Optionally filtered to a namespace prefix (mirrors targeting the .Base.UI / module
// component library rather than app plumbing).
public sealed class ComponentCatalog
{
    private readonly List<ComponentInfo> _components = new();
    public IReadOnlyList<ComponentInfo> Components => _components;

    public ComponentCatalog(IEnumerable<Assembly> assemblies, RazorSourceIndex sources, string? namespaceFilter = null)
    {
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

            foreach (var t in types)
            {
                if (t is null || t.IsAbstract || !typeof(IComponent).IsAssignableFrom(t)) continue;
                if (namespaceFilter is not null && t.Namespace?.StartsWith(namespaceFilter, StringComparison.Ordinal) != true) continue;

                var pars = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.IsDefined(typeof(ParameterAttribute)) || p.IsDefined(typeof(CascadingParameterAttribute)))
                    .Select(ToParamInfo)
                    .ToArray();

                _components.Add(new ComponentInfo(t.Name, asm.GetName().Name ?? "", sources.Find(t.Name), pars));
            }
        }
        _components.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }

    private static ParamInfo ToParamInfo(PropertyInfo p)
    {
        var pt = p.PropertyType;
        var (kind, options) = Classify(p.Name, pt);
        return new ParamInfo(p.Name, FriendlyType(pt), kind, options);
    }

    private static (ParamKind, string[]?) Classify(string name, Type pt)
    {
        if (pt == typeof(RenderFragment))
            return (name == "ChildContent" ? ParamKind.ChildContent : ParamKind.Slot, null);
        if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(RenderFragment<>))
            return (ParamKind.TemplatedSlot, null);
        if (pt == typeof(EventCallback) || (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(EventCallback<>)))
            return (ParamKind.Event, null);

        var underlying = Nullable.GetUnderlyingType(pt) ?? pt;
        if (underlying.IsEnum) return (ParamKind.Enum, Enum.GetNames(underlying));
        if (underlying.IsPrimitive || underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(Guid))
            return (ParamKind.Primitive, null);

        return (ParamKind.Complex, null);
    }

    private static string FriendlyType(Type t)
    {
        var u = Nullable.GetUnderlyingType(t);
        if (u is not null) return FriendlyType(u) + "?";
        if (t.IsGenericType)
        {
            var name = t.Name[..t.Name.IndexOf('`')];
            var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyType));
            return $"{name}<{args}>";
        }
        return t == typeof(string) ? "string"
             : t == typeof(int) ? "int"
             : t == typeof(bool) ? "bool"
             : t == typeof(double) ? "double"
             : t.Name;
    }
}
