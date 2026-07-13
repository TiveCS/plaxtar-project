using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Plaxtar.Designer;

// A parameter's editing kind (SPEC §6). Drives both the props panel and codegen.
public enum ParamKind { Primitive, Enum, ChildContent, Slot, TemplatedSlot, Event, Complex }

public sealed record ParamInfo(string Name, string Type, ParamKind Kind, string[]? EnumOptions, bool Bindable = false);
public sealed record ComponentInfo(string Name, string Assembly, string? Src, IReadOnlyList<ParamInfo> Params);

// Reflects the given assemblies for component types and their [Parameter] metadata.
// Optionally filtered to a namespace prefix (mirrors targeting the .Base.UI / module
// component library rather than app plumbing).
public sealed class ComponentCatalog
{
    private readonly List<ComponentInfo> _components = new();
    public IReadOnlyList<ComponentInfo> Components => _components;

    // Available Shells (#29): LayoutComponentBase subclasses across the scanned
    // assemblies. Collected before the component namespaceFilter, since layouts live
    // outside the component-library namespace (e.g. Base.UI/MainLayout).
    private readonly List<string> _shells = new();
    public IReadOnlyList<string> Shells => _shells;

    public ComponentCatalog(IEnumerable<Assembly> assemblies, RazorSourceIndex sources, string? namespaceFilter = null)
    {
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

            foreach (var t in types)
            {
              // A single member-load failure (TypeLoadException / FileNotFoundException on a
              // type whose deps can't resolve — routine in large third-party assemblies like
              // DevExpress) must not abort the whole scan, or every layout/component after the
              // bad type silently vanishes (e.g. an empty Shell list). Skip the type, keep going.
              try
              {
                if (t is null || t.IsAbstract) continue;

                // A "shell" is anything LayoutView can host: a LayoutComponentBase, OR —
                // for apps whose layouts use a custom base (e.g. Len's CommonPage) — any
                // component exposing a public [Parameter] RenderFragment Body, which is all
                // LayoutView actually needs. Detected shells are recorded and NOT also listed
                // as palette components (a layout isn't a draggable content component).
                if (IsShell(t))
                {
                    if (!_shells.Contains(t.Name)) _shells.Add(t.Name);
                    continue;
                }

                if (!typeof(IComponent).IsAssignableFrom(t)) continue;
                if (namespaceFilter is not null && t.Namespace?.StartsWith(namespaceFilter, StringComparison.Ordinal) != true) continue;

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.IsDefined(typeof(ParameterAttribute)) || p.IsDefined(typeof(CascadingParameterAttribute)))
                    .ToArray();

                // A param P is @bind-able iff the component also exposes a `PChanged`
                // EventCallback (the Blazor two-way binding convention).
                var changed = props
                    .Where(p => p.Name.EndsWith("Changed", StringComparison.Ordinal)
                                && (p.PropertyType == typeof(EventCallback)
                                    || (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(EventCallback<>))))
                    .Select(p => p.Name[..^"Changed".Length])
                    .ToHashSet(StringComparer.Ordinal);

                // A `XChanged` EventCallback backing a bindable param X is folded into
                // X's Bindable flag (@bind-X), not surfaced as its own editable event.
                var paramNames = props.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                bool IsBindingBacker(PropertyInfo p) =>
                    p.Name.EndsWith("Changed", StringComparison.Ordinal)
                    && changed.Contains(p.Name[..^"Changed".Length])
                    && paramNames.Contains(p.Name[..^"Changed".Length]);

                var pars = props
                    .Where(p => !IsBindingBacker(p))
                    .Select(p => ToParamInfo(p, changed.Contains(p.Name)))
                    .ToArray();

                _components.Add(new ComponentInfo(t.Name, asm.GetName().Name ?? "", sources.Find(t.Name), pars));
              }
              catch { /* unresolvable type — skip it, don't sink the rest of the scan */ }
            }
        }
        _components.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        _shells.Sort(StringComparer.Ordinal);
    }

    // True for a component LayoutView can host as a Shell: a standard LayoutComponentBase,
    // or a custom-base layout that still exposes a public [Parameter] RenderFragment Body.
    private static bool IsShell(Type t)
    {
        if (typeof(LayoutComponentBase).IsAssignableFrom(t)) return true;
        if (!typeof(IComponent).IsAssignableFrom(t)) return false;
        var body = t.GetProperty("Body", BindingFlags.Public | BindingFlags.Instance);
        return body is { PropertyType: var pt } && pt == typeof(RenderFragment)
            && body.IsDefined(typeof(ParameterAttribute));
    }

    private static ParamInfo ToParamInfo(PropertyInfo p, bool bindable)
    {
        var pt = p.PropertyType;
        var (kind, options) = Classify(p.Name, pt);
        return new ParamInfo(p.Name, FriendlyType(pt), kind, options, bindable);
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
        // A nested type (e.g. an enum declared inside a component) keeps its declaring
        // chain so the agent writes Badge.BadgeVariant, not a dangling BadgeVariant.
        if (t.IsNested && !t.IsGenericParameter)
            return FriendlyType(t.DeclaringType!) + "." + t.Name;

        return t == typeof(string) ? "string"
             : t == typeof(int) ? "int"
             : t == typeof(bool) ? "bool"
             : t == typeof(double) ? "double"
             : t.Name;
    }
}
