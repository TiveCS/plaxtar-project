using System.Text.Json;

namespace Plaxtar.Designer;

// Scoped editing state for one open screen: the mutable tree, selection, and
// mutations. Raises Changed so the canvas/props re-render. Loads from and saves
// to the plaxtar.designer/v1 file format.
public sealed class DesignSession
{
    private readonly ComponentTypeResolver _resolver;
    private readonly ComponentCatalog _catalog;
    private readonly PlaxtarDesignerOptions _options;

    public DesignSession(ComponentTypeResolver resolver, ComponentCatalog catalog, PlaxtarDesignerOptions options)
    {
        _resolver = resolver;
        _catalog = catalog;
        _options = options;
        Shell = options.DefaultShellName;
    }

    public string Screen { get; private set; } = "untitled";
    public string State { get; private set; } = "default";
    public string? Shell { get; private set; }
    public EditableNode Root { get; private set; } = new() { Element = "div" };
    public string? SelectedId { get; private set; }

    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    // The slot new nodes get inserted into for the selected container:
    // null = default ChildContent; otherwise a named RenderFragment.
    public string? ActiveSlot { get; private set; }

    public ComponentInfo? Info(string component) => _catalog.Components.FirstOrDefault(c => c.Name == component);
    public bool CanContain(string component) => Info(component)?.Params.Any(p => p.Kind == ParamKind.ChildContent) == true;
    public IReadOnlyList<string> NamedSlots(string? component) =>
        component is null ? Array.Empty<string>()
        : Info(component)?.Params.Where(p => p.Kind == ParamKind.Slot).Select(p => p.Name).ToList() ?? new();
    public bool CanContainNode(EditableNode n) =>
        n.IsElement || (n.Component is { } c && (CanContain(c) || NamedSlots(c).Count > 0));

    public EditableNode? Selected => SelectedId is null ? null : Find(Root, SelectedId);

    public void Select(string? id) { SelectedId = id; ActiveSlot = null; Notify(); }
    public void SelectSlot(string? slot) { ActiveSlot = slot; Notify(); }

    public void New(string screen, string? shell)
    {
        Screen = string.IsNullOrWhiteSpace(screen) ? "untitled" : screen.Trim();
        State = "default";
        Shell = shell;
        Root = new EditableNode { Element = "div" };   // neutral root, not tied to a Stack component
        SelectedId = null;
        Notify();
    }

    public void Add(string component) => Place(new EditableNode { Component = component, Src = Info(component)?.Src });
    public void AddElement(string tag) => Place(new EditableNode { Element = tag });

    private void Place(EditableNode node)
    {
        // Add into the selected node if it accepts children, else into its parent, else root.
        var sel = Selected;
        if (sel is not null && CanContainNode(sel))
        {
            if (ActiveSlot is { } slot)
            {
                if (!sel.Slots.TryGetValue(slot, out var list)) sel.Slots[slot] = list = new();
                list.Add(node);
            }
            else sel.Children.Add(node);
        }
        else if (sel is not null)
            (Parent(Root, sel) ?? Root).Children.Add(node);
        else
            Root.Children.Add(node);

        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

    public void SetClass(string id, string? value)
    {
        if (Find(Root, id) is { } n) { n.CssClass = string.IsNullOrWhiteSpace(value) ? null : value; Notify(); }
    }

    public void SetStyle(string id, string? value)
    {
        if (Find(Root, id) is { } n) { n.Style = string.IsNullOrWhiteSpace(value) ? null : value; Notify(); }
    }

    public void Remove(string id)
    {
        var node = Find(Root, id);
        if (node is null) return;

        // Deleting the root resets it to an empty <div> (root must always exist,
        // but you're never locked into the type it started as).
        if (node == Root)
        {
            Root = new EditableNode { Element = "div" };
            SelectedId = null;
            Notify();
            return;
        }

        ContainingList(Root, node)?.Remove(node);
        if (SelectedId == id) SelectedId = null;
        Notify();
    }

    public void Move(string id, int dir)
    {
        var node = Find(Root, id);
        var list = node is null ? null : ContainingList(Root, node);
        if (node is null || list is null) return;

        var i = list.IndexOf(node);
        var j = i + dir;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        Notify();
    }

    public void SetParam(string id, string name, object? value)
    {
        var node = Find(Root, id);
        if (node is null) return;
        node.Params[name] = value;
        Notify();
    }

    public async Task LoadAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<ScreenDoc>(fs)
                  ?? throw new InvalidOperationException($"Invalid screen doc: {path}");
        Screen = doc.Screen;
        State = doc.State;
        Shell = doc.Shell;
        Root = ToEditable(doc.Root);
        SelectedId = null;
        Notify();
    }

    public async Task<string> SaveAsync(string designsDir)
    {
        var doc = new Dictionary<string, object?>
        {
            ["schema"] = "plaxtar.designer/v1",
            ["screen"] = Screen,
            ["state"] = State,
            ["fe"] = _options.Fe,
            ["shell"] = Shell,
            ["root"] = ToDto(Root),
        };
        Directory.CreateDirectory(designsDir);
        var path = Path.Combine(designsDir, $"{Screen}.{State}.json");
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, doc, new JsonSerializerOptions { WriteIndented = true });
        return path;
    }

    private EditableNode ToEditable(NodeDto dto)
    {
        if (dto.Element is not null)
        {
            return new EditableNode
            {
                Element = dto.Element,
                CssClass = dto.CssClass,
                Style = dto.Style,
                Children = dto.Children.Select(ToEditable).ToList(),
            };
        }

        var type = _resolver.Resolve(dto.Component!);
        var node = new EditableNode { Component = dto.Component, Src = dto.Src };
        foreach (var (key, el) in dto.Params)
        {
            var prop = type.GetProperty(key);
            if (prop is null) continue;
            node.Params[key] = ParamCodec.FromJson(el, prop.PropertyType);
        }
        node.Children = dto.Children.Select(ToEditable).ToList();
        if (dto.Slots is not null)
            foreach (var (slot, kids) in dto.Slots)
                node.Slots[slot] = kids.Select(ToEditable).ToList();
        return node;
    }

    private object ToDto(EditableNode n)
    {
        var dict = new Dictionary<string, object?>();

        if (n.IsElement)
        {
            dict["element"] = n.Element;
            if (n.CssClass is not null) dict["class"] = n.CssClass;
            if (n.Style is not null) dict["style"] = n.Style;
            if (n.Children.Count > 0) dict["children"] = n.Children.Select(ToDto).ToList();
            return dict;
        }

        var type = _resolver.Resolve(n.Component!);
        dict["component"] = n.Component;
        if (n.Src is not null) dict["src"] = n.Src;

        if (n.Params.Count > 0)
        {
            var pars = new Dictionary<string, object?>();
            foreach (var (key, value) in n.Params)
            {
                var prop = type.GetProperty(key);
                pars[key] = prop is not null ? ParamCodec.ToJson(value, prop.PropertyType) : value;
            }
            dict["params"] = pars;
        }
        if (n.Children.Count > 0)
            dict["children"] = n.Children.Select(ToDto).ToList();

        var filledSlots = n.Slots.Where(s => s.Value.Count > 0).ToList();
        if (filledSlots.Count > 0)
            dict["slots"] = filledSlots.ToDictionary(s => s.Key, s => (object)s.Value.Select(ToDto).ToList());

        return dict;
    }

    private static EditableNode? Find(EditableNode n, string id) =>
        n.Id == id ? n : n.AllChildren.Select(c => Find(c, id)).FirstOrDefault(x => x is not null);

    private static EditableNode? Parent(EditableNode n, EditableNode target) =>
        n.AllChildren.Contains(target) ? n : n.AllChildren.Select(c => Parent(c, target)).FirstOrDefault(x => x is not null);

    // The list (default Children or a named slot) that directly holds `target`.
    private static List<EditableNode>? ContainingList(EditableNode n, EditableNode target)
    {
        if (n.Children.Contains(target)) return n.Children;
        foreach (var list in n.Slots.Values)
            if (list.Contains(target)) return list;
        foreach (var child in n.AllChildren)
            if (ContainingList(child, target) is { } found) return found;
        return null;
    }
}
