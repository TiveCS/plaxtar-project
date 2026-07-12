# Plaxtar codegen guide — Design Tree → `.razor`

You are an AI agent turning **Plaxtar Design Trees** into real Blazor `.razor` pages.
This is the complete, self-contained convention. It is safe to paste into a
consumer repo's `CONTEXT.md` / `CLAUDE.md` / `AGENTS.md`.

Plaxtar exports plain files — there is no server. Read them directly:

- `designs/<screen>.<state>.json` — a **Design Tree** (schema `plaxtar.designer/v1`).
- `designs/_catalog.json` — the **Catalog manifest**: every available component, its
  params (name + CLR type + kind), and source `.razor` path. Use it to resolve
  `@using` imports and param types.
- `designs/<screen>.flow.json` (optional) — Flow-editor box positions only. **Ignore it**;
  it is never codegen input.

The tree is authoritative for **component identity, params, and nesting**. A sibling
`.png` (if present) is a visual cross-check only — never infer structure from it.

## Design Tree schema (`plaxtar.designer/v1`)

```jsonc
{
  "schema": "plaxtar.designer/v1",
  "screen": "audit-log", "state": "default",
  "fe": "UI.Audit",
  "shell": "Base.UI/MainLayout",   // @layout for the generated page (null = none)
  "route": "/audit/log",            // @page route for the generated page (optional)
  "root": <Node>,                   // content-region tree (renders into the Shell @Body)
  "overlays": [<Node>]              // state-specific overlays (e.g. an open Modal), optional
}
```

### Node kinds

A `Node` is one of three kinds:

**element** — a raw HTML tag.
```jsonc
{ "element": "nav", "class": "topnav",
  "style": "gap:12px",                          // raw inline style (escape hatch)
  "layout": { "display": "flex", "gap": 12 },   // structured CSS flow (see map below)
  "attributes": { "aria-label": "Primary", "data-testid": "main-nav" }, // passthrough
  "children": [<Node>] }
// void tags (img/input/br/hr) carry no children and self-close
```

**text** — literal content (HTML-encoded, emitted verbatim, no wrapper).
```jsonc
{ "text": "Sign in" }
```

**component** — a real Blazor component.
```jsonc
{
  "component": "AuditLogTable",                 // type name as written in markup
  "src": "UI.Audit/Components/AuditLogTable.razor",
  "params": { "Striped": true, "PageSize": 25, "Range": { "$enum": "AuditRange.Last7Days" } },
  "bindings": { "SelectedId": "selectedId" },   // @bind-<Name> -> field name
  "events": {                                   // EventCallback -> { handler?, to? }
    "OnRowClick": { "handler": "HandleRowClick" }
  },
  "children": [<Node>],                          // default RenderFragment (ChildContent)
  "slots": { "HeaderContent": [<Node>] }         // named RenderFragments
}
```

### Value encodings (param values)

- `{ "$enum": "Type.Member" }` → `Type.Member` (declaring-type qualified, e.g. `Badge.BadgeVariant.Ok`)
- `{ "$bind": "field" }` → a field reference
- `{ "$raw": "new Foo{…}" }` → a raw C# expression (escape hatch)
- anything else is a JSON literal (string / bool / number)

## Codegen contract

1. Emit `@page "<route>"` (if present) and `@layout <shell>` (map `shell` to the layout
   type; drop the assembly prefix if it is a namespace, e.g. `Base.UI/MainLayout` → `MainLayout`).
2. Emit `@using` for each distinct namespace/assembly referenced by nodes (resolve from
   each node's `src` and from `_catalog.json`).
3. Walk `root`:
   - **element** → `<tag class="…" style="…" attr="…"> … </tag>`.
   - **text** → its content verbatim (HTML-encoded, no wrapper).
   - **component** → `<Component Param="…" @bind-X="field" OnX="Handler"> … </Component>`.
   - `params` → attributes. Enums → `Param="Type.Member"`. Strings/bools/numbers → literals.
   - `bindings` → `@bind-<Name>="field"`.
   - `attributes` (element only) → each key/value verbatim on the tag (`data-testid="…"`,
     `aria-*`, `id`, `href`, …); a valueless entry (`""`) emits a boolean attribute.
   - `children` → nested markup. `slots.<Name>` → `<Name> … </Name>`.
   - `layout` (element only) → inline `style`, one declaration per key using the map below.
     A raw `style` string (if present) is appended and wins on conflict.

   **`layout` key → CSS map:** `display`→`display`, `direction`→`flex-direction`,
   `wrap`→`flex-wrap`, `justify`→`justify-content`, `align`→`align-items`, `gap`→`gap`,
   `columns`→`grid-template-columns`, `rows`→`grid-template-rows`,
   `width`/`minWidth`/`maxWidth`→`width`/`min-width`/`max-width`, `padding`→`padding`,
   `margin`→`margin`, `position`→`position`, `top`/`right`/`bottom`/`left`→same.
   Values are literal CSS tokens (`"12px"`, `"repeat(2,1fr)"`, `"sticky"`). Prefer the
   project's spacing utility classes over inline style if any exist.

4. `overlays` → render conditionally on the relevant state flag (see Transitions).
5. Emit `@code` stubs: a field per `bindings` value; a `bool` flag per same-screen
   Transition target; a method per `events` value (performing any transition inline),
   each marked `// TODO: implement` for the remaining logic.

## Events and Transitions

Each `events` value is **always an object** `{ handler?, to? }` — never a bare string.

- `handler` names the `@code` method to wire (`OnX="Handler"`). When only `to` is present,
  synthesize a handler name (e.g. `OpenModalOpen`).
- `to` is a **Transition** — where firing this event goes. Two forms:

### Same-screen transition (bare name)

`"to": "modal-open"` targets a **sibling State** of this screen. Realize it by diffing
`<screen>.<current>` against `<screen>.<to>`: the difference is normally an added
`overlays` entry (e.g. an open Modal) or a changed condition. Generate a `bool` flag,
set it in the handler, and render the target State's overlay under `@if (_flag)`.

```jsonc
// audit-log.default.json  (button node)
{ "component": "UIButton",
  "events": { "OnClick": { "handler": "OpenDetails", "to": "modal-open" } },
  "children": [ { "text": "Details" } ] }
```
```razor
@* generated *@
<UIButton OnClick="OpenDetails">Details</UIButton>
@if (_modalOpen)
{
    @* the overlay from audit-log.modal-open.json's `overlays` *@
    <DetailsModal OnClose="() => _modalOpen = false" />
}
@code {
    private bool _modalOpen;
    private void OpenDetails() { _modalOpen = true; /* TODO: implement */ }
}
```

### Cross-screen transition (dotted name)

`"to": "audit-detail.default"` targets **another screen** → real navigation. Inject
`NavigationManager` and navigate to that screen's `route` (read from the target file,
`designs/audit-detail.default.json`). If the target has no `route`, leave a `// TODO: route`.

```jsonc
{ "component": "AuditRow",
  "events": { "OnRowGo": { "to": "audit-detail.default" } } }
```
```razor
@inject NavigationManager Nav
<AuditRow OnRowGo="GoAuditDetail" />
@code {
    private void GoAuditDetail() => Nav.NavigateTo("/audit/detail"); // route from audit-detail.default.json
}
```

Transitions are **authored + exported only** — Plaxtar never runs them. You generate the
real wiring. Only component nodes (their `EventCallback` params) carry a `to`.
