# Plaxtar Designer — Specification

Status: draft · Scope: MVP + explicitly-deferred items · Companion docs: [CONTEXT.md](./CONTEXT.md), [docs/adr/](./docs/adr/)

---

## 1. Problem & goals

### 1.1 Problem

The team builds Blazor UI **code-first**: components exist (`.razor`) but no design system is defined, and there is no design source of truth. Communicating a desired UI to an AI agent in prose is too abstract — the agent guesses component identity, props, spacing, and structure, and drifts. Figma is the usual answer but: (a) it **redraws** components as vectors, so its output permanently drifts from real code; (b) its MCP access is paywalled/limited.

### 1.2 Goal

A dev-only visual **Composer** that assembles the team's **already-coded** Blazor components into screens, rendered **live**, and exports a **codegen-grade JSON tree** an AI agent consumes over **MCP** to generate the real `.razor` page. The thing on the canvas *is* the real component (via `DynamicComponent`), so "exact UI/UX" is literal, not approximate.

### 1.3 Non-goals (MVP)

- Not a Figma replacement: no vector drawing, no freeform x/y canvas, no prototyping animations.
- No **design-system/token** authoring (color/type/spacing scales, variants). Deferred — see §16.
- No **multi-screen flows** (linked navigation/routing between screens). Deferred.
- No React support in this tool (the React path is solved externally via shadcn kits).
- Not a runtime overlay on a live page — it is a dedicated design route.

### 1.4 Success criteria

1. In a real FE app running in dev, open `/design`, drop real components into the real Shell, set params, see live render — with the app's real auth/DI/API/DB available.
2. Export a screen+state to `designs/<screen>.<state>.json`.
3. An AI agent reads that JSON via MCP and generates a `.razor` page that matches the designed screen 1:1 (component identity, params, nesting, layout).

---

## 2. Personas & primary workflow

**Persona:** a module dev (e.g. Audit team) who runs their own FE subset in dev and wants to hand an exact UI to an AI agent.

**Primary workflow:**

1. Run the FE app in dev (`dotnet watch`), e.g. `.UI.Audit` on `:44549`.
2. Navigate to `/audit/design` (dev-only route).
3. Pick or create a **Screen**; choose a **Shell** (`@layout`).
4. Drop components from the **Catalog** into the canvas; nest into containers and component slots.
5. Set params in the **Props** panel; flip **States** (default / modal-open / loading / empty) to design each.
6. **Export** → writes `designs/<screen>.<state>.json` (+ optional `.png`) into the repo.
7. Tell the AI agent "build `designs/audit-log.json`"; agent reads via MCP → emits real `.razor`.

---

## 3. Architecture

### 3.1 In-process, dev-only (see ADR 0001)

The Composer ships as a NuGet package `Plaxtar.Designer` and runs **inside the target FE app's own process**, exposing a dev-only route (default `/design`). It is **not** a standalone canvas app referencing only the component assembly. Rationale: the real components/Shell need live **auth, API, DB** just to render; running in-process means all of that is already wired (no service mocking).

### 3.2 Per-FE installation & Catalog scoping (see ADR 0004)

Each micro-frontend is a **separate process = separate assembly set**. Install the package **per FE**. The **Catalog** is the reflection over assemblies loaded in *that* process:

```
run .UI.Audit  →  process loads  .Base.UI (shared)  +  .UI.Audit (module)
Catalog        =  shared components  +  audit-specific components
```

Modules you don't run (`.UI.LMS`, `.UI.KMS`) are absent — correct, you're not designing them. Push shared components **and the Shell** into `.Base.UI` so they appear in every FE's Catalog and render in-process.

### 3.3 Data flow (see ADR 0003)

```
[Composer @ /design] ──export──> designs/*.json (+ *.png) in repo ──read──> [MCP server] ──> [AI agent] ──> real .razor
```

Exports are files in the repo: git-versioned, work offline, decoupled from a running app. The MCP server reads files; it does **not** connect to the running app.

### 3.4 Component map

| Part | Responsibility |
|---|---|
| **Catalog** | Reflect loaded assemblies → list Components + `[Parameter]` metadata + source `.razor` path. Drives palette + Props panel. |
| **Canvas** | Render the chosen **Shell** live; render the **Design Tree** into the Shell's `@Body` via `DynamicComponent`; host selection/drag/drop. |
| **Props panel** | Edit selected node's parameters, typed from reflection. |
| **Screens list** | Manage independent Screens (open/new/switch/rename/delete). One open at a time; all persist as files. |
| **States** | Named variants of a Screen (default/modal-open/loading/empty/custom); each exports its own tree. |
| **Export** | Serialize Design Tree → JSON (+ screenshot) to `designs/`. |
| **MCP server** | Read `designs/` → serve catalog + screen trees to the agent. |

---

## 4. Rendering model

- **Instantiation:** `<DynamicComponent Type="@t" Parameters="@dict" />` renders any component by `Type` with a parameter dictionary at runtime. The Design Tree is rendered by recursively emitting `DynamicComponent` per node.
- **Catalog discovery:** reflection over loaded assemblies for types implementing `IComponent`. For each, reflect public properties carrying `[Parameter]` / `[CascadingParameter]`; capture name, CLR type, whether it's a `RenderFragment` (slot), whether it's an `EventCallback`, enum options, default value, and the declaring type's source path.
- **Source path:** resolve each component's `.razor` path (best-effort via type namespace → project layout convention, or an optional generated manifest at build time). Carried into the Catalog and JSON so the agent knows imports/where the component lives.
- **Live:** composing mutates in-memory tree → re-render is immediate (Blazor state change). Editing component **markup/CSS** hot-reloads via `dotnet watch`. Editing component **structure** (`[Parameter]` signature, new component) needs rebuild (Catalog is reflected at load).

---

## 5. Layout model (see ADR 0002)

**Flow / DOM**, not absolute coordinates. Components are arranged in real CSS containers; the export maps 1:1 to real markup.

- **Containers:** `Stack` (flex column/row), `Grid` (CSS grid), plus any container components from `.Base.UI`. Editable via panel: `display`, `flex-direction`, `gap`, `justify`, `align`, `wrap`, `grid-template-columns/rows`, `width`/`min`/`max` (px/%/fr/auto), padding/margin.
- **Positioning:** `position: static|relative|sticky|fixed|absolute` is a **CSS property on a node**, scoped to its container — used exactly where real code uses it (floating nav, tooltips, overlays). This is what earlier discussion called "hybrid"; it is included in the flow model.
- **Not supported:** pinning every element to raw x/y with no container semantics (the "absolute canvas" that drifts).
- **Self-styling components:** components that manage their own layout (nav, sidebar, modal) require **no** layout editing — placed in the tree, they position themselves. The panel only edits the page-scaffold glue between components.
- **Panel, not code:** layout props are edited through controls (toggles, sliders, segmented pickers, number+unit). A raw-CSS escape-hatch field exists but is not the primary path.

---

## 6. Parameter editing model

For each `[Parameter]` on the selected node, the Props panel renders a typed editor. MVP support:

| Param kind | Editor | Serialized as | Codegen emits |
|---|---|---|---|
| `string` | text input | `"value"` | `Prop="value"` |
| `bool` | toggle | `true`/`false` | `Prop="true"` |
| numeric (`int`,`double`,`decimal`) | number input | number | `Prop="25"` |
| `enum` | segmented / dropdown | `"EnumValue"` | `Prop="MyEnum.Value"` |
| `RenderFragment` (default child content) | **nesting** — drop components into the node | `children: [...]` | nested markup inside the tag |
| named `RenderFragment` (e.g. `HeaderContent`) | named slot drop zone | `slots: { HeaderContent: [...] }` | `<HeaderContent>…</HeaderContent>` |
| `RenderFragment<T>` (templated) | **deferred** (v1: placeholder note) | `{ "templated": true }` | `<Template Context="ctx">…</Template>` TODO |
| `EventCallback` / `EventCallback<T>` | handler-name field (no live handler at design time) | `{ "handler": "OnSave" }` | `Prop="OnSave"` + `// TODO: implement` |
| complex object / generic `T` | **deferred** (v1: raw JSON / bound-field name) | `{ "raw": "..." }` or `{ "bind": "field" }` | `@bind-Prop="field"` or literal, agent resolves |
| two-way bindable (`Value`+`ValueChanged`) | detect pair → bind field name | `{ "bind": "field" }` | `@bind-Value="field"` |

Notes:
- **Slots are the nesting mechanism.** Modal body, Card content, layout wrappers are all `RenderFragment` slots. The tree represents them as `children` (default) or named `slots`.
- EventCallbacks have no behavior at design time; they render as inert. The export records the intended handler name so the agent wires a stub.

---

## 7. Design Tree JSON schema

### 7.1 File

One file per **(screen, state)**: `designs/<screen>.<state>.json`. `<state>` = `default` for the base state.

### 7.2 Shape

```jsonc
{
  "schema": "plaxtar.designer/v1",
  "screen": "audit-log",           // stable id, kebab-case
  "state": "default",              // default | modal-open | loading | empty | <custom>
  "fe": "UI.Audit",                // originating FE (assembly short name)
  "shell": "Base.UI/MainLayout",   // @layout the content renders inside (null = none)
  "route": "/audit/log",           // intended runtime route for the generated page (optional)
  "root": <Node>,                  // the content-region tree (rendered into Shell @Body)
  "overlays": [<Node>],            // state-specific overlays (e.g. an open Modal), optional
  "screenshot": "audit-log.default.png", // optional, sibling file
  "meta": { "createdBy": "...", "updatedAt": "2026-07-10T..." }
}
```

### 7.3 Node

```jsonc
{
  "component": "AuditLogTable",                     // type name as used in markup
  "src": "UI.Audit/Components/AuditLogTable.razor", // source path for imports
  "assembly": "UI.Audit",                           // declaring assembly
  "params": {                                        // literal/bound [Parameter] values
    "Striped": true,
    "PageSize": 25,
    "Range": { "$enum": "AuditRange.Last7Days" }
  },
  "bindings": { "SelectedId": "selectedId" },        // @bind-* → field names
  "events": { "OnRowClick": "HandleRowClick" },      // EventCallback → handler names
  "layout": {                                        // CSS applied to this node's wrapper (optional)
    "display": "flex", "direction": "column",
    "gap": 14, "width": "100%", "position": "static"
  },
  "children": [<Node>],                              // default RenderFragment content
  "slots": { "HeaderContent": [<Node>] }             // named RenderFragments
}
```

Value encodings: enums as `{ "$enum": "Type.Member" }`; raw/complex as `{ "$raw": "new Foo{…}" }`; bound field as `{ "$bind": "fieldName" }`. Everything else is a JSON literal.

### 7.4 Example (audit-log, default) — abbreviated

```jsonc
{
  "schema": "plaxtar.designer/v1", "screen": "audit-log", "state": "default",
  "fe": "UI.Audit", "shell": "Base.UI/MainLayout", "route": "/audit/log",
  "root": { "component": "Stack", "layout": { "direction": "column", "gap": 14 }, "children": [
    { "component": "PageHeader", "src": "Base.UI/Layout/PageHeader.razor",
      "params": { "Title": "Audit Log", "ActionText": "+ New Entry" },
      "events": { "OnAction": "OpenNewEntry" } },
    { "component": "AuditFilterBar", "src": "UI.Audit/Components/AuditFilterBar.razor",
      "params": { "Range": { "$enum": "AuditRange.Last7Days" }, "ShowSearch": true } },
    { "component": "AuditLogTable", "src": "UI.Audit/Components/AuditLogTable.razor",
      "params": { "Striped": true, "PageSize": 25 },
      "bindings": { "SelectedId": "selectedId" } }
  ] }
}
```

---

## 8. Persistence, Screens & States

- **Store:** JSON files under `designs/` in the FE repo, committed to git.
- **Naming:** `<screen>.<state>.json` (+ optional `<screen>.<state>.png`). `screen` is a stable kebab-case id; display name kept in file `meta` (or a `designs/index.json`).
- **Screens are independent.** Designing many unrelated pages = many files. One Screen open on the canvas at a time; all others persist untouched (editor-tabs model). Switching loads a different file — never clears.
- **New / open / switch / rename / delete** via the Screens list.
- **States:** each Screen has ≥1 State. `default` always exists. Additional States (`modal-open`, `loading`, `empty`, custom) each serialize to their own file and can differ in `root`/`overlays`/params.
- **No linked flows** in MVP: a Screen cannot navigate to another. Deferred.

---

## 9. MCP server

A small MCP server (stdio) run by the agent, pointed at the repo. It reads `designs/` and the Catalog manifest; it does not touch the running app.

### 9.1 Tools

| Tool | Input | Output |
|---|---|---|
| `list_screens` | — | `[{ screen, states[], fe, shell, route, updatedAt }]` |
| `get_screen` | `screen`, `state?` (default `default`) | full Design Tree JSON (§7) |
| `get_catalog` | `fe?` | `[{ component, src, assembly, params:[{name,type,kind,enumOptions?,default?}] }]` |
| `get_screenshot` | `screen`, `state?` | path/bytes of the PNG (optional) |

### 9.2 Agent contract (codegen)

Given `get_screen`, the agent generates a `.razor` page:

1. Emit `@page "<route>"` and `@layout <shell>` (from `shell`).
2. `@using` for each distinct `assembly`/namespace referenced by nodes (`src`/`assembly`).
3. Walk `root`: for each node emit `<Component Param="…" @bind-X="…" On…="Handler">`; recurse `children` as inner markup; emit named `slots` as `<SlotName>…</SlotName>`.
4. Apply `layout` as classes/inline style (or map to the project's spacing utilities — project convention).
5. Emit `@code` stubs for `bindings` fields and `events` handlers, each marked `// TODO: implement`.
6. `overlays` render conditionally on the relevant State flag.

The tree is authoritative for **structure, identity, params**; the screenshot is a visual cross-check, never the source of truth.

---

## 10. Dev-only gating (see ADR 0001)

Keep `/design` out of production. Layered:

1. **Conditional package reference (primary):** reference `Plaxtar.Designer` only in `Debug`:
   ```xml
   <ItemGroup Condition="'$(Configuration)'=='Debug'">
     <PackageReference Include="Plaxtar.Designer" Version="…" />
   </ItemGroup>
   ```
   Release build → assembly physically absent → no route, no code.
2. **Route registration gating (belt+suspenders):** add the designer assembly to the Router's `AdditionalAssemblies` only under `#if DEBUG`; otherwise its `@page "/design"` never registers.
3. **Runtime env check (optional):** `if (env.IsDevelopment()) app.MapDesigner();`.

Layer 1 alone suffices; add 2 if Debug-config builds are ever shipped.

---

## 11. Package & repo structure

```
Plaxtar/                         (this repo)
├── CONTEXT.md
├── SPEC.md
├── docs/adr/*.md
├── src/
│   ├── Plaxtar.Designer/        NuGet: Composer route, Catalog, Canvas, Props, Export
│   └── Plaxtar.Designer.Mcp/    MCP server reading designs/
└── sample/
    └── SampleHost/              tiny Blazor app + dummy components to develop against
```

- **Develop** against `sample/SampleHost` (no heavy deps) for a fast local loop.
- **Validate** by installing the NuGet into the real office FE (heavy deps live).
- The sample host is a dev harness only; never shipped, never referenced by real apps.

---

## 12. MVP scope vs deferred

**MVP (build):**
- In-process `/design` route, dev-only gated.
- Catalog via reflection (params: primitives, enums, RenderFragment slots, EventCallback names).
- Canvas: render Shell + Design Tree live via `DynamicComponent`; select/drag/drop into containers and slots.
- Flow layout with panel-edited CSS incl. `position`.
- Screens list (open/new/switch/rename/delete); States (default + modal/loading/empty).
- Export Design Tree JSON (+ optional screenshot) to `designs/`.
- MCP server: `list_screens`, `get_screen`, `get_catalog`.

**Deferred:**
- Design-system/token authoring & variants (§16).
- Multi-screen linked flows / routing between screens.
- Templated `RenderFragment<T>` and complex-object param editors (beyond raw/bind).
- Live MCP into a running app (files-only in MVP).
- React support.

---

## 13. Risks & open questions

1. **Render feasibility (highest):** does `DynamicComponent` + the real Shell render cleanly in one FE process with heavy auth/API/DB live? → **Spike first** (§15).
2. **Source-path resolution:** reliably mapping a component `Type` → `.razor` path (needed for agent imports). May require a build-time manifest.
3. **DI/cascading values:** some components need `[CascadingParameter]` (auth state, themes) — canvas must provide the real cascade (in-process helps, but design-region wrapping must not strip it).
4. **Drag-drop UX for nesting/slots:** clean drop targets for containers vs named slots.
5. **Complex param editing:** objects/generics deferred; confirm real components don't require them for the target screens.
6. **Screenshot capture** inside a Blazor app (headless render vs browser capture) — optional, decide later.

---

## 14. Milestones

1. **Spike:** in `sample/SampleHost`, render a hardcoded tree via `DynamicComponent` inside a Shell; then repeat inside the real office FE with real deps. Gate the whole project on this.
2. **Catalog:** reflection → component list + param metadata + Props panel (read-only render).
3. **Canvas MVP:** drop + select + set primitive/enum params; live render.
4. **Nesting:** container layout editing + RenderFragment slots.
5. **Screens/States + Export:** file persistence, screenshot optional.
6. **MCP server:** tools + agent codegen contract; end-to-end test (design → export → agent emits matching `.razor`).

---

## 15. Verification (end-to-end)

- **Spike gate:** run the FE in dev, hit `/design`, confirm a `DynamicComponent`-rendered real component appears inside the real Shell with live services.
- **Round-trip test:** design a known screen → export JSON → have the agent generate `.razor` from `get_screen` → diff generated page against a hand-written reference; assert component identity, params, nesting, layout match.
- **Prod-safety test:** build in `Release`; assert `/design` returns 404 and the designer assembly is absent from output.
