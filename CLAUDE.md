# Plaxtar Designer — repo guide for AI agents

This repo builds a dev-only visual **Composer** that assembles real Blazor components into screens and exports them as JSON the agent turns into real `.razor` pages. See `SPEC.md`, `CONTEXT.md`, and `docs/adr/`.

## How designs reach you (no MCP)

Designs are plain files in this repo. Read them directly — there is no server.

- `designs/<screen>.<state>.json` — a **Design Tree** (schema `plaxtar.designer/v1`).
- `designs/_catalog.json` — the **Catalog manifest**: available components, their params (name + CLR type + kind), and source `.razor` paths. Use it to resolve `@using`/imports and param types.

When asked to "build `designs/<screen>.<state>.json`", read that file **and** `designs/_catalog.json`, then generate the `.razor` page per the contract below.

## Design Tree schema (`plaxtar.designer/v1`)

```jsonc
{
  "schema": "plaxtar.designer/v1",
  "screen": "audit-log", "state": "default",
  "fe": "UI.Audit",
  "shell": "Base.UI/MainLayout",     // @layout for the generated page (null = none)
  "route": "/audit/log",              // @page route (optional)
  "root": <Node>,                     // content-region tree, renders into the Shell @Body
  "overlays": [<Node>]                // state-specific overlays (e.g. an open Modal), optional
}
```

A `Node` is one of three kinds: a **component** (`component`), a raw HTML **element** (`element`, with optional `class`/`style`/`layout`/`attributes`), or a **text** node (`text`, literal content). Element nodes codegen to `<tag class="…" style="…" data-…="…">children</tag>`; text nodes emit their content verbatim (HTML-encoded). Mixed content is an element with a text child plus element children.

```jsonc
// element node (attributes = passthrough data-*/aria-*/id/href…, element-only)
{ "element": "nav", "class": "topnav", "attributes": { "aria-label": "Primary", "data-testid": "main-nav" }, "children": [<Node>] }
// text node
{ "text": "Sign in" }
// icon = element + class (Font Awesome): { "element": "i", "class": "fa fa-user" }
// void tags (img/input/br/hr) carry no children
```

`Node` (component form):
```jsonc
{
  "component": "AuditLogTable",       // type name as written in markup
  "src": "UI.Audit/Components/AuditLogTable.razor",
  "params": { "Striped": true, "PageSize": 25, "Range": { "$enum": "AuditRange.Last7Days" } },
  "bindings": { "SelectedId": "selectedId" },   // @bind-* -> field name
  "events": {                                   // EventCallback -> { handler?, to? } (object form, always)
    "OnRowClick": { "handler": "HandleRowClick" },        // handler stub only
    "OnOpen":     { "handler": "Open", "to": "modal-open" },   // + Transition to a sibling State
    "OnRowGo":    { "to": "audit-detail.default" }            // Transition to another Screen (nav)
  },
  "layout": { "display": "flex", "direction": "column", "gap": 14, "width": "100%", "position": "static" },
  "children": [<Node>],               // default RenderFragment (ChildContent)
  "slots": { "HeaderContent": [<Node>] } // named RenderFragments
}
```

Value encodings: `{ "$enum": "Type.Member" }` · `{ "$bind": "field" }` · `{ "$raw": "new Foo{…}" }` · everything else is a JSON literal.

## Codegen contract (Design Tree -> .razor)

1. Emit `@page "<route>"` (if present) and `@layout <shell>` (map `shell` to the layout type; drop the assembly prefix if it's a namespace).
2. Emit `@using` for each distinct namespace/assembly referenced by nodes (resolve from `src`/`_catalog.json`).
3. Walk `root`: for an **element** node emit `<tag class="…" style="…" attr="…"> … </tag>`; for a **text** node emit its `text` verbatim (HTML-encoded, no wrapper); for a **component** node emit `<Component Param="…" @bind-X="field" OnX="Handler"> … </Component>`.
   - `params` -> attributes. Enums -> `Param="Type.Member"`. Strings/bools/numbers -> literals.
   - `bindings` -> `@bind-<Name>="field"`. `events` -> see step 3a below (`events` values are objects `{ handler?, to? }`).
   - `attributes` (element nodes) -> each key/value emitted verbatim on the tag (`data-testid="…"`, `aria-*`, `id`, `href`, …); a valueless entry (`""`) emits a boolean attribute. Void tags (`img`/`input`/`br`/`hr`) self-close with no children.
   - `children` -> nested markup inside the tag. `slots.<Name>` -> `<Name> … </Name>`.
   - `layout` (element nodes) -> inline `style`, one declaration per key using this map: `display`→`display`, `direction`→`flex-direction`, `wrap`→`flex-wrap`, `justify`→`justify-content`, `align`→`align-items`, `gap`→`gap`, `columns`→`grid-template-columns`, `rows`→`grid-template-rows`, `width`/`minWidth`/`maxWidth`→`width`/`min-width`/`max-width`, `padding`→`padding`, `margin`→`margin`, `position`→`position`, `top`/`right`/`bottom`/`left`→same. A raw `style` string (if present) is appended and wins on conflict. Values are literal CSS tokens (e.g. `"12px"`, `"repeat(2,1fr)"`, `"sticky"`). Prefer the project's spacing utility classes over inline style if any exist.
   3a. **`events` values are objects `{ handler?, to? }`** (never bare strings). Wire `<Name>="Handler"` where `Handler` is the method the agent emits (from `handler`, or a synthesized name when only `to` is present). `to` is a **Transition** (ADR 0008) the agent realizes:
       - **Bare name** (`"to": "modal-open"`) = a sibling **State** of this Screen. Realize by diffing `<screen>.default` vs `<screen>.<to>`: the diff is normally an added `overlays` entry (an open Modal) or a changed flag. Generate a `bool` field and set it in the handler; render the target State's overlay under `@if (_flag)`. (This is the same flag `overlays` render on — step 4.)
       - **Dotted name** (`"to": "audit-detail.default"`) = another **Screen**. Realize as navigation: `@inject NavigationManager Nav`, and in the handler `Nav.NavigateTo("<route>")` where `<route>` is the target Screen's `route` (read from `designs/audit-detail.default.json`). If the target has no `route`, leave a `// TODO: route` note.
       - An event with only `to` (no `handler`) still needs a method: synthesize one (e.g. `OpenModalOpen`) that performs the transition.
4. `overlays` -> render conditionally on the relevant state flag (the same flag a same-Screen Transition toggles — see 3a).
5. Emit `@code` stubs: a field per `bindings` value; a `bool` flag per same-Screen Transition target; a method per `events` value (performing any `to` transition inline), each marked `// TODO: implement` for the remaining business logic.

The tree is authoritative for **component identity, params, and nesting**. A sibling `.png` (if present) is a visual cross-check only — never infer structure from it.

## Building the tool itself

Work is tracked as GitHub issues (tracer-bullet slices) on `TiveCS/plaxtar-project`. Start each with the render primitive `sample/SampleHost/Designer/TreeNodeRenderer.razor`. The sample host (`sample/SampleHost`, Blazor Server, net10.0) is the dev harness; validate against a real FE separately.
