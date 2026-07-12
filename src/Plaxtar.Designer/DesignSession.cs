using System.Text.Json;

namespace Plaxtar.Designer;

// Drop position relative to a target node (#23): a sibling before/after it, or a
// first-class child (Into, containers only).
public enum DropPos { Before, Into, After }

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
    // State-specific overlays (e.g. an open Modal) stacked over the content region.
    public List<EditableNode> Overlays { get; private set; } = new();
    public string? SelectedId { get; private set; }

    // Every top-level tree the session edits: the content root plus each overlay.
    private IEnumerable<EditableNode> Roots => Overlays.Prepend(Root);

    public event Action? Changed;

    // --- Undo/redo (#27). Snapshot-based: every structural Notify checkpoints the tree;
    // selection/panel changes call Notify(false) and are not undoable. History clears on
    // load/new/state-switch and is depth-capped. ---
    private const int MaxHistory = 100;
    private readonly List<Snap> _undo = new();
    private readonly List<Snap> _redo = new();
    private Snap? _lastSnapshot;
    private sealed record Snap(EditableNode Root, List<EditableNode> Overlays);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    private Snap Snapshot() => new(CloneNode(Root), Overlays.Select(CloneNode).ToList());

    private void Notify(bool structural = true)
    {
        if (structural)
        {
            if (_lastSnapshot is not null)
            {
                _undo.Add(_lastSnapshot);
                if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
                _redo.Clear();
            }
            _lastSnapshot = Snapshot();
        }
        Changed?.Invoke();
    }

    private void ResetHistory() { _undo.Clear(); _redo.Clear(); _lastSnapshot = Snapshot(); }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(Snapshot());
        var prev = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        ApplySnapshot(prev);
        _lastSnapshot = Snapshot();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(Snapshot());
        var next = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
        ApplySnapshot(next);
        _lastSnapshot = Snapshot();
        Changed?.Invoke();
    }

    private void ApplySnapshot(Snap s)
    {
        Root = CloneNode(s.Root);
        Overlays = s.Overlays.Select(CloneNode).ToList();
        if (SelectedId is not null && FindAny(SelectedId) is null) { SelectedId = null; ActiveSlot = null; }
    }

    // The slot new nodes get inserted into for the selected container:
    // null = default ChildContent; otherwise a named RenderFragment.
    public string? ActiveSlot { get; private set; }

    public ComponentInfo? Info(string component) => _catalog.Components.FirstOrDefault(c => c.Name == component);
    public bool CanContain(string component) => Info(component)?.Params.Any(p => p.Kind == ParamKind.ChildContent) == true;
    public IReadOnlyList<string> NamedSlots(string? component) =>
        component is null ? Array.Empty<string>()
        : Info(component)?.Params.Where(p => p.Kind == ParamKind.Slot).Select(p => p.Name).ToList() ?? new();
    public bool CanContainNode(EditableNode n) =>
        (n.IsElement && !n.IsVoidElement) || (n.Component is { } c && (CanContain(c) || NamedSlots(c).Count > 0));

    public EditableNode? Selected => SelectedId is null ? null : FindAny(SelectedId);

    public void SetState(string state) { if (!string.IsNullOrWhiteSpace(state)) { State = state.Trim(); Notify(false); } }

    // Set the Screen's Shell (#29): a layout name, or null for a blank canvas / no @layout.
    public void SetShell(string? shell) { Shell = string.IsNullOrWhiteSpace(shell) ? null : shell; Notify(false); }

    public void Select(string? id) { SelectedId = id; ActiveSlot = null; Notify(false); }
    public void SelectSlot(string? slot) { ActiveSlot = slot; Notify(false); }

    public void New(string screen, string? shell)
    {
        Screen = string.IsNullOrWhiteSpace(screen) ? "untitled" : screen.Trim();
        State = "default";
        Shell = shell;
        Root = new EditableNode { Element = "div" };   // neutral root, not tied to a Stack component
        Overlays = new();
        SelectedId = null;
        ResetHistory();
        Notify(false);
    }

    public void Add(string component) => Place(new EditableNode { Component = component, Src = Info(component)?.Src });
    public void AddElement(string tag) => Place(new EditableNode { Element = tag.Trim().TrimStart('<').TrimEnd('>') });
    public void AddText(string text = "text") => Place(new EditableNode { Text = text });

    // Icon quick-add (#25): an <i> whose class is the Font Awesome name, no children.
    public void AddIcon(string? faClass) =>
        Place(new EditableNode { Element = "i", CssClass = string.IsNullOrWhiteSpace(faClass) ? "fa fa-star" : faClass.Trim() });

    // Add a top-level overlay (e.g. a Modal) for the current State.
    public void AddOverlay(string component)
    {
        var node = new EditableNode { Component = component, Src = Info(component)?.Src };
        Overlays.Add(node);
        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

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
            (ParentAny(sel) ?? Root).Children.Add(node);
        else
            Root.Children.Add(node);

        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

    // Deep-clone a node (fresh Ids). Params values are primitives / immutable
    // BindExpr|RawExpr records, so a shallow value copy is safe.
    public EditableNode CloneNode(EditableNode src) => new()
    {
        Component = src.Component, Element = src.Element, Text = src.Text, Src = src.Src,
        CssClass = src.CssClass, Style = src.Style,
        Layout = new(src.Layout), Attributes = new(src.Attributes),
        Params = new(src.Params), Bindings = new(src.Bindings), Events = new(src.Events),
        Children = src.Children.Select(CloneNode).ToList(),
        Slots = src.Slots.ToDictionary(kv => kv.Key, kv => kv.Value.Select(CloneNode).ToList()),
    };

    // Context-menu insert (#22): into a named `slot` of the target, else its
    // ChildContent if it's a container, else as the target's next sibling.
    public void InsertInto(string targetId, string? slot, EditableNode node)
    {
        var target = FindAny(targetId);
        if (target is null) { Root.Children.Add(node); }
        else if (slot is { } s && target.Component is { } c && NamedSlots(c).Contains(s))
        {
            if (!target.Slots.TryGetValue(s, out var list)) target.Slots[s] = list = new();
            list.Add(node);
        }
        else if (CanContainNode(target)) target.Children.Add(node);
        else if (ListOf(target) is { } siblings) siblings.Insert(siblings.IndexOf(target) + 1, node);
        else Root.Children.Add(node);

        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

    public void InsertComponentInto(string targetId, string? slot, string component) =>
        InsertInto(targetId, slot, new EditableNode { Component = component, Src = Info(component)?.Src });
    public void InsertElementInto(string targetId, string? slot, string tag) =>
        InsertInto(targetId, slot, new EditableNode { Element = tag.Trim().TrimStart('<').TrimEnd('>') });
    public void InsertTextInto(string targetId, string? slot, string text = "text") =>
        InsertInto(targetId, slot, new EditableNode { Text = text });

    // Clone a node as its own following sibling.
    public void Duplicate(string id)
    {
        var node = FindAny(id);
        if (node is null) return;
        var copy = CloneNode(node);
        if (ListOf(node) is { } list) list.Insert(list.IndexOf(node) + 1, copy);
        else (ParentAny(node) ?? Root).Children.Add(copy);
        SelectedId = copy.Id;
        Notify();
    }

    // Copy an existing node into the State's overlays (non-destructive).
    public void AddNodeAsOverlay(string id)
    {
        if (FindAny(id) is not { } node) return;
        var copy = CloneNode(node);
        Overlays.Add(copy);
        SelectedId = copy.Id;
        ActiveSlot = null;
        Notify();
    }

    // --- Positional drag/drop (#23, ADR 0007). Before/After = sibling of target;
    // Into = first-class child of a container. The JS drag layer resolves the zone
    // and calls one of these once, on drop. No schema change (only child order). ---

    // Place `node` relative to `target` at `pos`. Null target = append to root.
    private void PlaceAt(string? targetId, DropPos pos, EditableNode node)
    {
        var target = targetId is null ? null : FindAny(targetId);
        if (target is null) { Root.Children.Add(node); }
        else if (pos == DropPos.Into && CanContainNode(target)) { target.Children.Add(node); }
        else
        {
            var list = ListOf(target) ?? Root.Children;
            var idx = list.IndexOf(target);
            if (idx < 0) list.Add(node);
            else list.Insert(pos == DropPos.After ? idx + 1 : idx, node);
        }
        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

    public void DropNewAt(string? targetId, DropPos pos, EditableNode node) => PlaceAt(targetId, pos, node);

    // Move an existing node to (target, pos). No-op into its own subtree.
    public void MoveNodeTo(string dragId, string? targetId, DropPos pos)
    {
        var drag = FindAny(dragId);
        if (drag is null) return;
        var target = targetId is null ? null : FindAny(targetId);
        if (target is not null && IsSelfOrDescendant(drag, target)) return;

        ListOf(drag)?.Remove(drag);   // detach first, then index the target's list

        if (target is null) { Root.Children.Add(drag); }
        else if (pos == DropPos.Into && CanContainNode(target)) { target.Children.Add(drag); }
        else
        {
            var list = ListOf(target) ?? Root.Children;
            var idx = list.IndexOf(target);
            if (idx < 0) list.Add(drag);
            else list.Insert(pos == DropPos.After ? idx + 1 : idx, drag);
        }
        SelectedId = drag.Id;
        Notify();
    }

    public void SetClass(string id, string? value)
    {
        if (FindAny(id) is { } n) { n.CssClass = string.IsNullOrWhiteSpace(value) ? null : value; Notify(); }
    }

    public void SetStyle(string id, string? value)
    {
        if (FindAny(id) is { } n) { n.Style = string.IsNullOrWhiteSpace(value) ? null : value; Notify(); }
    }

    public void SetText(string id, string? value)
    {
        if (FindAny(id) is { IsText: true } n) { n.Text = value ?? ""; Notify(); }
    }

    // Inline element text (#24): the element's leading/only Text child, as a single
    // field. Empty removes it; otherwise edits it in place or appends one (so it lands
    // after e.g. a leading <i> icon). Mixed content stays fully editable as nodes.
    public string ElementText(EditableNode n) => n.Children.FirstOrDefault(c => c.IsText)?.Text ?? "";

    public void SetElementText(string id, string? text)
    {
        if (FindAny(id) is not { IsElement: true, IsVoidElement: false } n) return;
        var existing = n.Children.FirstOrDefault(c => c.IsText);
        if (string.IsNullOrEmpty(text))
        {
            if (existing is not null) n.Children.Remove(existing);
        }
        else if (existing is not null) existing.Text = text;
        else n.Children.Add(new EditableNode { Text = text });
        Notify();
    }

    // Passthrough attribute on an element (data-*/aria-*/id...). A null value removes
    // it; an empty string keeps it valueless (e.g. `required`, `disabled`).
    public void SetAttribute(string id, string key, string? value)
    {
        if (FindAny(id) is not { } n || string.IsNullOrWhiteSpace(key)) return;
        if (value is null) n.Attributes.Remove(key);
        else n.Attributes[key.Trim()] = value;
        Notify();
    }

    // Rename an attribute key (preserving value); drops the old key.
    public void RenameAttribute(string id, string oldKey, string newKey)
    {
        if (FindAny(id) is not { } n || !n.Attributes.TryGetValue(oldKey, out var v)) return;
        n.Attributes.Remove(oldKey);
        if (!string.IsNullOrWhiteSpace(newKey)) n.Attributes[newKey.Trim()] = v;
        Notify();
    }

    // Structured layout prop (SPEC §5): set a `layout` key (display, gap, position…);
    // empty value removes it. Values are literal CSS tokens ("column", "12px", "sticky").
    public void SetLayout(string id, string key, string? value)
    {
        if (FindAny(id) is not { } n) return;
        if (string.IsNullOrWhiteSpace(value)) n.Layout.Remove(key);
        else n.Layout[key] = value.Trim();
        Notify();
    }

    public void ClearLayout(string id)
    {
        if (FindAny(id) is { } n) { n.Layout.Clear(); Notify(); }
    }

    public void Remove(string id)
    {
        var node = FindAny(id);
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

        // A top-level overlay is removed from the overlays list directly.
        if (Overlays.Remove(node))
        {
            if (SelectedId == id) SelectedId = null;
            Notify();
            return;
        }

        ContainingListAny(node)?.Remove(node);
        if (SelectedId == id) SelectedId = null;
        Notify();
    }

    public void Move(string id, int dir)
    {
        var node = FindAny(id);
        var list = node is null ? null
                 : Overlays.Contains(node) ? Overlays
                 : ContainingListAny(node);
        if (node is null || list is null) return;

        var i = list.IndexOf(node);
        var j = i + dir;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        Notify();
    }

    // --- Drag & drop (#14) ---------------------------------------------------

    // Drop a NEW node (from the Catalog/Elements) relative to a target: into the
    // target if it's a container, else as the next sibling after it, else at root.
    public void DropNew(string? targetId, EditableNode node)
    {
        var target = targetId is null ? null : FindAny(targetId);
        if (target is not null && CanContainNode(target))
            target.Children.Add(node);
        else if (target is not null && ListOf(target) is { } list)
            list.Insert(list.IndexOf(target) + 1, node);
        else
            Root.Children.Add(node);
        SelectedId = node.Id;
        ActiveSlot = null;
        Notify();
    }

    public void DropComponent(string? targetId, string component) =>
        DropNew(targetId, new EditableNode { Component = component, Src = Info(component)?.Src });
    public void DropElement(string? targetId, string tag) =>
        DropNew(targetId, new EditableNode { Element = tag });

    // Move an EXISTING node relative to a target: into it if a container, else as the
    // target's next sibling. No-op if dropping a node into its own subtree.
    public void MoveNode(string dragId, string? targetId)
    {
        var drag = FindAny(dragId);
        if (drag is null) return;

        var target = targetId is null ? null : FindAny(targetId);
        if (target is not null && IsSelfOrDescendant(drag, target)) return;

        ListOf(drag)?.Remove(drag);
        if (target is null)
            Root.Children.Add(drag);
        else if (CanContainNode(target))
            target.Children.Add(drag);
        else if (ListOf(target) is { } list)
            list.Insert(list.IndexOf(target) + 1, drag);
        else
            Root.Children.Add(drag);

        SelectedId = dragId;
        Notify();
    }

    // The list (default Children, a named slot, or the overlays root) directly holding n.
    private List<EditableNode>? ListOf(EditableNode n) =>
        Overlays.Contains(n) ? Overlays : ContainingListAny(n);

    private static bool IsSelfOrDescendant(EditableNode ancestor, EditableNode node) =>
        ancestor == node || ancestor.AllChildren.Any(c => IsSelfOrDescendant(c, node));

    public void SetParam(string id, string name, object? value)
    {
        var node = FindAny(id);
        if (node is null) return;
        node.Params[name] = value;
        node.Bindings.Remove(name);   // a literal value replaces any @bind on the same param
        Notify();
    }

    // @bind-<name>="field": records the bound field name; clears the literal so the
    // param exports under `bindings`, not `params`. Empty field removes the binding.
    public void SetBinding(string id, string name, string? field)
    {
        var node = FindAny(id);
        if (node is null) return;
        if (string.IsNullOrWhiteSpace(field)) node.Bindings.Remove(name);
        else { node.Bindings[name] = field.Trim(); node.Params.Remove(name); }
        Notify();
    }

    // <Name>="Handler" for an EventCallback param. Empty handler removes the event.
    public void SetEvent(string id, string name, string? handler)
    {
        var node = FindAny(id);
        if (node is null) return;
        if (string.IsNullOrWhiteSpace(handler)) node.Events.Remove(name);
        else node.Events[name] = handler.Trim();
        Notify();
    }

    // Complex-object param: a BindExpr ($bind field) or RawExpr ($raw expression),
    // stored in Params so it exports inside `params`. Null clears it.
    public void SetComplex(string id, string name, object? marker)
    {
        var node = FindAny(id);
        if (node is null) return;
        if (marker is null) node.Params.Remove(name);
        else node.Params[name] = marker;
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
        Overlays = doc.Overlays?.Select(ToEditable).ToList() ?? new();
        SelectedId = null;
        ResetHistory();
        Notify(false);
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
        if (Overlays.Count > 0) doc["overlays"] = Overlays.Select(ToDto).ToList();
        Directory.CreateDirectory(designsDir);
        var path = Path.Combine(designsDir, $"{Screen}.{State}.json");
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, doc, new JsonSerializerOptions { WriteIndented = true });
        return path;
    }

    // Start a new State variant for the current screen. `clone` seeds it from the
    // current tree/overlays (e.g. modal-open = default + a Modal) so states share a
    // base; otherwise it starts empty. `default` always exists as the base state.
    public void NewState(string state, bool clone = true)
    {
        var name = string.IsNullOrWhiteSpace(state) ? "state" : state.Trim();
        if (clone)
        {
            Root = Clone(Root);
            Overlays = Overlays.Select(Clone).ToList();
        }
        else
        {
            Root = new EditableNode { Element = "div" };
            Overlays = new();
        }
        State = name;
        SelectedId = null;
        ResetHistory();
        Notify(false);
    }

    // Deep-copy a node (new Ids) so a cloned State edits independently of its source.
    private EditableNode Clone(EditableNode n) => new()
    {
        Component = n.Component,
        Element = n.Element,
        Text = n.Text,
        Src = n.Src,
        CssClass = n.CssClass,
        Style = n.Style,
        Layout = new(n.Layout),
        Attributes = new(n.Attributes),
        Params = new(n.Params),
        Bindings = new(n.Bindings),
        Events = new(n.Events),
        Children = n.Children.Select(Clone).ToList(),
        Slots = n.Slots.ToDictionary(s => s.Key, s => s.Value.Select(Clone).ToList()),
    };

    private EditableNode ToEditable(NodeDto dto)
    {
        if (dto.Text is not null)
            return new EditableNode { Text = dto.Text };

        if (dto.Element is not null)
        {
            return new EditableNode
            {
                Element = dto.Element,
                CssClass = dto.CssClass,
                Style = dto.Style,
                Layout = dto.Layout is null ? new() : new(dto.Layout),
                Attributes = dto.Attributes is null ? new() : new(dto.Attributes),
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
        if (dto.Bindings is not null)
            foreach (var (name, field) in dto.Bindings) node.Bindings[name] = field;
        if (dto.Events is not null)
            foreach (var (name, handler) in dto.Events) node.Events[name] = handler;
        node.Children = dto.Children.Select(ToEditable).ToList();
        if (dto.Slots is not null)
            foreach (var (slot, kids) in dto.Slots)
                node.Slots[slot] = kids.Select(ToEditable).ToList();
        return node;
    }

    private object ToDto(EditableNode n)
    {
        var dict = new Dictionary<string, object?>();

        if (n.IsText)
        {
            dict["text"] = n.Text;
            return dict;
        }

        if (n.IsElement)
        {
            dict["element"] = n.Element;
            if (n.CssClass is not null) dict["class"] = n.CssClass;
            if (n.Style is not null) dict["style"] = n.Style;
            if (n.Layout.Count > 0) dict["layout"] = new Dictionary<string, string>(n.Layout);
            if (n.Attributes.Count > 0) dict["attributes"] = new Dictionary<string, string>(n.Attributes);
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
        if (n.Bindings.Count > 0) dict["bindings"] = new Dictionary<string, string>(n.Bindings);
        if (n.Events.Count > 0) dict["events"] = new Dictionary<string, string>(n.Events);
        if (n.Children.Count > 0)
            dict["children"] = n.Children.Select(ToDto).ToList();

        var filledSlots = n.Slots.Where(s => s.Value.Count > 0).ToList();
        if (filledSlots.Count > 0)
            dict["slots"] = filledSlots.ToDictionary(s => s.Key, s => (object)s.Value.Select(ToDto).ToList());

        return dict;
    }

    // Traversal across every root (content + overlays).
    private EditableNode? FindAny(string id) => Roots.Select(r => Find(r, id)).FirstOrDefault(x => x is not null);
    private EditableNode? ParentAny(EditableNode t) => Roots.Select(r => Parent(r, t)).FirstOrDefault(x => x is not null);
    private List<EditableNode>? ContainingListAny(EditableNode t) => Roots.Select(r => ContainingList(r, t)).FirstOrDefault(x => x is not null);

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
