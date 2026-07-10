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

`Node`:
```jsonc
{
  "component": "AuditLogTable",       // type name as written in markup
  "src": "UI.Audit/Components/AuditLogTable.razor",
  "params": { "Striped": true, "PageSize": 25, "Range": { "$enum": "AuditRange.Last7Days" } },
  "bindings": { "SelectedId": "selectedId" },   // @bind-* -> field name
  "events": { "OnRowClick": "HandleRowClick" }, // EventCallback -> handler name
  "layout": { "display": "flex", "direction": "column", "gap": 14, "width": "100%", "position": "static" },
  "children": [<Node>],               // default RenderFragment (ChildContent)
  "slots": { "HeaderContent": [<Node>] } // named RenderFragments
}
```

Value encodings: `{ "$enum": "Type.Member" }` · `{ "$bind": "field" }` · `{ "$raw": "new Foo{…}" }` · everything else is a JSON literal.

## Codegen contract (Design Tree -> .razor)

1. Emit `@page "<route>"` (if present) and `@layout <shell>` (map `shell` to the layout type; drop the assembly prefix if it's a namespace).
2. Emit `@using` for each distinct namespace/assembly referenced by nodes (resolve from `src`/`_catalog.json`).
3. Walk `root`: for each node emit `<Component Param="…" @bind-X="field" OnX="Handler"> … </Component>`.
   - `params` -> attributes. Enums -> `Param="Type.Member"`. Strings/bools/numbers -> literals.
   - `bindings` -> `@bind-<Name>="field"`. `events` -> `<Name>="Handler"`.
   - `children` -> nested markup inside the tag. `slots.<Name>` -> `<Name> … </Name>`.
   - `layout` -> classes or inline `style` (prefer the project's spacing utilities if any exist).
4. `overlays` -> render conditionally on the relevant state flag.
5. Emit `@code` stubs: a field per `bindings` value; a method per `events` value, each marked `// TODO: implement`.

The tree is authoritative for **component identity, params, and nesting**. A sibling `.png` (if present) is a visual cross-check only — never infer structure from it.

## Building the tool itself

Work is tracked as GitHub issues (tracer-bullet slices) on `TiveCS/plaxtar-project`. Start each with the render primitive `sample/SampleHost/Designer/TreeNodeRenderer.razor`. The sample host (`sample/SampleHost`, Blazor Server, net10.0) is the dev harness; validate against a real FE separately.
