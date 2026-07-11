# Plaxtar Designer — Specification

Status: draft · Scope: MVP + explicitly-deferred items · Companion docs: [CONTEXT.md](./CONTEXT.md), [docs/adr/](./docs/adr/)

---

## 1. Problem & goals

### 1.1 Problem

The team builds Blazor UI **code-first**: components exist (`.razor`) but no design system is defined, and there is no design source of truth. Communicating a desired UI to an AI agent in prose is too abstract — the agent guesses component identity, props, spacing, and structure, and drifts. Figma is the usual answer but: (a) it **redraws** components as vectors, so its output permanently drifts from real code; (b) its MCP access is paywalled/limited.

### 1.2 Goal

A dev-only visual **Composer** that assembles the team's **already-coded** Blazor components into screens, rendered **live**, and exports a **codegen-grade JSON tree** an AI agent reads (as a repo file) to generate the real `.razor` page. The thing on the canvas *is* the real component (via `DynamicComponent`), so "exact UI/UX" is literal, not approximate.

### 1.3 Non-goals (MVP)

- Not a Figma replacement: no vector drawing, no freeform x/y canvas, no prototyping animations.
- No **design-system/token** authoring (color/type/spacing scales, variants). Deferred — see §16.
- No **multi-screen flows** (linked navigation/routing between screens). Deferred.
- No React support in this tool (the React path is solved externally via shadcn kits).
- Not a runtime overlay on a live page — it is a dedicated design route.

### 1.4 Success criteria

1. In a real FE app running in dev, open `/design`, drop real components into the real Shell, set params, see live render — with the app's real auth/DI/API/DB available.
2. Export a screen+state to `designs/<screen>.<state>.json`.
3. An AI agent reads that JSON file (guided by `CLAUDE.md`) and generates a `.razor` page that matches the designed screen 1:1 (component identity, params, nesting, layout).

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
7. Tell the AI agent "build `designs/audit-log.json`"; agent reads the file (+ `designs/_catalog.json`) → emits real `.razor`.

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
[Composer @ /design] ──export──> designs/*.json (+ _catalog.json, *.png) in repo ──read──> [AI agent] ──> real .razor
```

Exports are files in the repo: git-versioned, work offline, decoupled from a running app. The agent reads the files directly (no MCP); `CLAUDE.md` carries the schema + codegen rules.

### 3.4 Component map

| Part | Responsibility |
|---|---|
| **Catalog** | Reflect loaded assemblies → list Components + `[Parameter]` metadata + source `.razor` path. Drives palette + Props panel. |
| **Canvas** | Render the chosen **Shell** live; render the **Design Tree** into the Shell's `@Body` via `DynamicComponent`; host selection/drag/drop. |
| **Props panel** | Edit selected node's parameters, typed from reflection. |
| **Screens list** | Manage independent Screens (open/new/switch/rename/delete). One open at a time; all persist as files. |
| **States** | Named variants of a Screen (default/modal-open/loading/empty/custom); each exports its own tree. |
| **Export** | Serialize Design Tree → JSON (+ screenshot) to `designs/`. |
| **Catalog manifest** | Export `designs/_catalog.json` from reflection so the agent has param types + import paths. |

---

## 4. Rendering model

- **Instantiation:** `<DynamicComponent Type="@t" Parameters="@dict" />` renders any component by `Type` with a parameter dictionary at runtime. The Design Tree is rendered by recursively emitting `DynamicComponent` per node.
- **Catalog discovery:** reflection over loaded assemblies for types implementing `IComponent`. For each, reflect public properties carrying `[Parameter]` / `[CascadingParameter]`; capture name, CLR type, whether it's a `RenderFragment` (slot), whether it's an `EventCallback`, enum options, default value, and the declaring type's source path.
- **Source path:** resolve each component's `.razor` path (best-effort via type namespace → project layout convention, or an optional generated manifest at build time). Carried into the Catalog and JSON so the agent knows imports/where the component lives.
- **Live:** composing mutates in-memory tree → re-render is immediate (Blazor state change). Editing component **markup/CSS** hot-reloads via `dotnet watch`. Editing component **structure** (`[Parameter]` signature, new component) needs rebuild (Catalog is reflected at load).
- **Interactivity gap (root cause, #14):** under the `/designer` page's render mode, DOM event handlers (`@onclick`, `@ondrop`, `@ondragstart`) are interactive only when **authored by the page itself** — page-level buttons and the `DynamicComponent` leaves are fine, but handlers a *custom child component* attaches to its own elements do **not** wire up. Pattern to avoid it: the canvas tree and its drag/drop targets are built by the page (a recursive `RenderFragment` on the page component, with `EventCallback.Factory.Create(this, …)` handlers), not delegated to a child renderer component. `ondragover` is `preventDefault`-only (no handler) so allowing a drop doesn't spam the Server circuit with a render per mousemove.

### 4.1 Canvas modes: Edit vs Preview (see ADR 0005)

The Canvas renders in two modes; **fidelity is defined by Preview, not Edit**. "Same as result" means Preview.

- **Edit mode** (default): each node is wrapped in a `<div class="pd-node">` selection box (hover outline, node-type tag, click-select), and the palette/props side panels are shown. Consequence: the content region is narrower than production, and the wrapper element sits between a node and its parent — so in a flex/grid parent the *wrapper* is the flex/grid item, and `width:100%`/`flex:1`/`gap`/`:first-child` can resolve differently. This drift is **cosmetic and edit-only**; it never reaches the export or the generated `.razor`.
- **Preview mode** (toggle): the side panels collapse (grid columns → `0 1fr 0`) so the content region gets its real desktop width, and the tree renders **raw** — no wrapper, no tag, no empty-slot placeholder, no selection `onclick`. The result is the exact DOM codegen would emit (same flex/grid item identity, same width resolution). The **Shell** (`LayoutView`) renders in both modes so `@Body` width matches production. Preview disables *design-selection* only; the live components themselves stay interactive.

Implementation is a single `_preview` flag branching the canvas render — no schema or codegen change. The two render paths must stay in sync with the codegen contract (§7), or Preview stops being trustworthy.

**Deferred — responsive/device fidelity:** media queries evaluate against the browser viewport, so Preview matches only the editor's own (desktop) width. Simulating breakpoint widths (1440/768/375) needs an **iframe** canvas that gives the design its own viewport; that is a separate, larger slice (a superset of Preview mode, not a reversal).

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

A **Node** is one of three kinds:

- **component** — as above (`component` + typed `params`/`bindings`/`events`/`slots`).
- **element** — a raw HTML tag: `{ "element": "nav", "class": "...", "style": "...", "layout": {…}, "attributes": { "data-testid": "main-nav", "aria-label": "Primary" }, "children": [<Node>] }`. `attributes` is a free map of passthrough attributes (`data-*`, `aria-*`, `id`, `href`, `role`, …), emitted verbatim, distinct from typed params. Void tags (`img`, `input`, `br`, `hr`) carry no children/text. **Attributes are element-only** — for a test hook on a component, wrap it in an element.
- **text** — literal content: `{ "text": "Sign in" }`. A first-class node so mixed content (`<p>Hello <b>world</b></p>` = element `p` with a text child + a `b` child) is expressible; codegen emits the text verbatim (HTML-encoded).

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

## 9. Agent access (files, no MCP)

The agent (Claude Code) reads the exported files **directly** — there is no MCP server in the MVP (ADR 0003, amended). A filesystem-capable agent re-reading a repo file through MCP adds nothing. MCP remains a possible future addition only for non-filesystem/remote/sandboxed agents.

### 9.1 The file contract

- `designs/<screen>.<state>.json` — the Design Tree (§7).
- `designs/_catalog.json` — Catalog manifest: `[{ component, src, assembly, params:[{name,type,kind,enumOptions?,default?}] }]`.
- `designs/<screen>.<state>.png` — optional visual cross-check.
- `CLAUDE.md` — the schema + codegen rules the agent auto-loads (the "decoder ring").

Workflow: user says "build `designs/<screen>.<state>.json`" → agent reads that file + `designs/_catalog.json` → generates `.razor` per the contract.

### 9.2 Agent contract (codegen)

Given a Design Tree file, the agent generates a `.razor` page:

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
│   └── (no MCP server — agent reads designs/ files directly)
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
- Canvas: render Shell + Design Tree live via `DynamicComponent`; select/drag/drop into containers and slots; Edit/Preview toggle where Preview is the raw, full-width, true-fidelity view (§4.1).
- Flow layout with panel-edited CSS incl. `position`.
- Screens list (open/new/switch/rename/delete); States (default + modal/loading/empty).
- Export Design Tree JSON (+ optional screenshot) to `designs/`.
- Catalog manifest `designs/_catalog.json` + codegen contract in `CLAUDE.md` (no MCP server).

**Deferred:**
- Design-system/token authoring & variants (§16).
- Multi-screen linked flows / routing between screens.
- Templated `RenderFragment<T>` and complex-object param editors (beyond raw/bind).
- MCP server entirely (files-only for a filesystem agent; add only for remote/sandboxed agents later).
- Responsive/device-width preview (iframe canvas with breakpoint presets); Preview mode (§4.1) covers desktop-width fidelity only.
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
6. **Catalog manifest + codegen contract** (`CLAUDE.md`); end-to-end test (design → export files → agent reads → emits matching `.razor`).

---

## 15. Verification (end-to-end)

- **Spike gate:** run the FE in dev, hit `/design`, confirm a `DynamicComponent`-rendered real component appears inside the real Shell with live services.
- **Round-trip test:** design a known screen → export JSON → have the agent generate `.razor` from `get_screen` → diff generated page against a hand-written reference; assert component identity, params, nesting, layout match.
- **Prod-safety test:** build in `Release`; assert `/design` returns 404 and the designer assembly is absent from output.

---

## 16. Designer UX & native-HTML essentials (planned)

Resolved in a grill session. Split into **essentials (build now)** and **UX polish (deferred to a dedicated Designer UI/UX grill)** — the owner has many more UX issues to work through, so catalog + drag are held until that pass.

### 16.1 Native elements + text nodes — *essential*
Real apps compose raw HTML, not only components. The tree already supports element nodes; add:
- **Text nodes** (schema §7.3): a third node kind, selectable/orderable, so `<p>`, `<h4>`, `<li>`, `<a>` get content and mixed content works.
- **Element palette**: curated groups — *Text* (`p`, `h1`–`h6`, `a`), *Inline* (`span`, `i`, `b`, `strong`, `em`), *Lists* (`ul`, `ol`, `li`), *Forms* (`form`, `label`, `input`, `select`, `option`, `button`, `textarea`), *Semantic* (`nav`, `header`, `footer`, `section`, `article`, `aside`), *Media* (`img`) — plus a **free-text "any tag"** box and a **Text** quick-add. Void tags (`img`/`input`/`br`/`hr`) render childless.
- **Icons:** the office FE renders icons as `<i class="fa fa-…">` (Font Awesome). No icon-specific model — `<i>` is a first-class palette entry and the glyph rides on the existing `class` field. (Font Awesome CSS must be loaded by the host for the glyph to show on the canvas; otherwise `<i>` renders empty but codegen is still correct.)

### 16.2 Passthrough attributes (element-only) — *essential*
Element nodes gain an `attributes` map (schema §7.3) for `data-*`, `aria-*`, `id`, `href`, `role`, `title`, … — edited via a key-value list in the Props panel, emitted verbatim in codegen, separate from typed params. Components don't take passthrough attributes (splat risk); wrap in an element for test hooks.

### 16.3 Catalog scaling — *deferred (Designer UI/UX grill)*
Direction: a **search/filter** box over the palette, components **grouped by namespace/assembly** into **collapsible** sections (containers marked); grouping is free from existing catalog metadata. Held pending the broader UI/UX pass, since the palette is part of a larger designer-UI rework.

### 16.4 Drag UX (JS-interop) — *deferred (Designer UI/UX grill)*
Direction: a **JS-interop drag layer** for instant feedback without per-mousemove Server round-trips — an **insertion line** (before/after by cursor position), target-container **glow** for drop-*into*, **move-out** insertion at the parent level; JS owns hover visuals, server called **once on drop** with `{ targetId, position: before | after | into }`; reorder is **insert** (not swap); drag JS ships as an RCL static asset. Held with §16.3 for the UX pass.
