# Plaxtar Designer

A dev-only visual **Composer** that assembles already-coded Blazor components into screens, live-rendered, and exports a codegen-grade JSON tree so an AI agent can generate the real `.razor` page. Built because communicating UI to an agent in prose is too abstract, and Figma redraws components (drift) with paywalled MCP.

## Language

**Composer**:
The visual tool where you drop real components into a screen and set their params. Not a documentation tool, not a vector editor.
_Avoid_: Figma clone, canvas editor, page builder (in prose it's fine, but the noun is "Composer")

**Component**:
An already-coded Blazor `.razor` component from the target app. Rendered live on the canvas via `DynamicComponent`, never redrawn or copied.
_Avoid_: widget, element, block

**Design Tree**:
The nested `{ type, params, children }` structure a screen composes to. It IS the component hierarchy (flow/DOM), not coordinates. Exported as JSON.
_Avoid_: layout, mockup, artboard

**Node kind**:
Every node in a Design Tree is one of three kinds. A **component** node is a real Blazor component (typed `params`, `bindings`, `events`, slots). An **element** node is a raw HTML tag (`class`, `style`, structured `layout`, and arbitrary passthrough `attributes` like `data-testid`/`aria-*`). A **text** node is literal text content — a first-class, selectable, orderable node, so mixed content (`<p>Hello <b>world</b></p>`) is expressible. Passthrough `attributes` are element-only; to put a test hook on a component, wrap it in an element. A component node's `events` value is an **object** `{ handler?, to? }` (normalized — no bare-string form): `handler` names the codegen method stub, `to` is a **Transition** target.
_Avoid_: widget, tag, leaf (use "component / element / text node")

**State**:
A named variant of a Screen with specific conditions applied (default, modal-open, error, empty). Each State exports as its own Design Tree. States are connected to one another by **Transitions**.
_Avoid_: variant, mode

**Transition**:
A directed edge from a **component** node's event (an `EventCallback` param, e.g. `OnClick`) to a target — either a sibling **State** of the same Screen (bare name, e.g. `modal-open`) or another Screen (dotted, e.g. `audit-detail.default`). Declares *intent* ("this click goes to modal-open"); it is **authored + exported only** — the Composer never executes it. Stored **on the triggering event** in the source State's Design Tree (target is a State/Screen name, never a node Id, since Ids are per-file). The agent realizes it: a same-Screen target by diffing the two States and toggling a flag / revealing an overlay; a cross-Screen target by navigating to that Screen's `route`.
_Avoid_: link, action, trigger (reserve those; the noun is "Transition")

**Simulate** (Play mode):
A third Canvas mode (beside Edit and Preview) that lets you *walk your authored **Transitions*** to feel a flow — click a wired control and it loads that Transition's target **State**. Deliberately **not a prototyping tool**: it invents no behavior, only navigates between States you already composed (each is a real render). It runs on a throwaway session loaded from the saved files, so it never touches your edits; every jump just loads `(screen, state)` and re-renders (no fake overlay animation). Its purpose is **verification** — catch a wrong flow before spending codegen tokens — not stakeholder prototyping.
_Avoid_: prototype, preview (Preview = fidelity check, Simulate = flow check), interactive mockup

**Flow view** (a.k.a. Flow graph):
The state-machine editor inside the Composer: **States** as boxes, each box exposing its component nodes' events as output **ports**, and **Transitions** drawn as arrows from a port to a target State box. Author + export only — not a clickable prototype, no live simulation. Box positions are editor-only metadata in a `designs/<screen>.flow.json` sidecar (`plaxtar.flow/v1`), never in the Design Tree (which stays coordinate-free) and never read by codegen.
_Avoid_: prototype, storyboard, wireflow

**Screen**:
The unit of design in the MVP: one page's content region plus its States, rendered inside a chosen Shell.
_Avoid_: page (page = the eventual real `.razor`; Screen = its design)

**Shell**:
The real Blazor layout (`@layout` / `MainLayout` with `@Body`) a Screen is designed inside. Always **recorded** on the Screen for codegen (`@layout` on the exported page). Whether it also renders live on the canvas depends on its kind: a **standard shell** (`LayoutComponentBase`) renders as a fixed backdrop; a **heavy shell** — app-chrome on a custom base (e.g. Len's `CommonPage`) with `position:fixed` nav + `AuthorizeView`/service calls — is recorded only, not rendered (it would overlap the Composer and can crash the circuit). See ADR 0010.
_Avoid_: layout (overloaded — reserve "layout" for CSS flow), frame, template

**Flow layout**:
Arranging components with real CSS primitives (flex, grid, width, `position: sticky/fixed/absolute`) via panel controls, exactly as real markup does. Opposite of pinning elements to raw x/y.
_Avoid_: absolute canvas, freeform

**Canvas**:
The live render of the Screen being composed. Has two modes. **Edit mode** wraps every node in a selection box for click-select and shows the palette/props panels — so the content region is narrower and each node carries an extra wrapper element (cosmetic layout drift, accepted for editing). **Preview mode** is the *true-WYSIWYG* view: it collapses the panels to give the content region its real desktop width and renders the tree **raw** — no wrappers, no decorations — so the DOM (and thus flex/grid item identity, width resolution, media-query behaviour) is identical to the generated `.razor`. Preview is non-interactive for design-selection; the live components themselves still work.
_Avoid_: viewport, artboard, stage. "Same as result" = Preview mode, not Edit mode.

**Catalog**:
The reflected list of available Components + their `[Parameter]` props + source `.razor` path, driving the props panel and giving the agent import info. **Scoped to the assemblies loaded in the running FE process** — i.e. `.Base.UI` (shared) + the module FE you run (e.g. `.UI.Audit`). Modules you don't run (`.UI.LMS`, `.UI.KMS`) are absent by design.
_Avoid_: registry, palette

**FE / Micro-frontend**:
One deployable Blazor app = one process = one assembly set (e.g. `.UI` main on `:45546`, `.UI.Audit` on `:44549`). Same domain in prod via proxy, but **separate processes** — they do not share a runtime. The Composer runs per-FE; its Catalog cannot see another FE's process.
_Avoid_: app, service

## Relationships

- A **Screen** is designed inside exactly one **Shell** and has one or more **States**
- **States** of a Screen form a graph via **Transitions**; a Transition targets a sibling State (same Screen) or another Screen (resolved to that Screen's `route` for navigation)
- Each **State** exports one **Design Tree** (JSON) + optional screenshot into the repo; a Screen with a hand-arranged **Flow view** also exports a `<screen>.flow.json` position sidecar (editor-only, ignored by codegen)
- A **Design Tree** is a nesting of **Components**, each configured from the **Catalog**
- The **MCP server** reads exported JSON files from the repo; the agent turns a **Design Tree** into a real `.razor` page
- The **Composer** ships as a NuGet package (`Plaxtar.Designer`) that runs **in-process inside the real app** (dev-only route `/design`)
- The Composer is installed **per FE**; its **Catalog** = shared `.Base.UI` components + the running module FE's own components. Shared components (and ideally the **Shell**) belong in `.Base.UI` so they appear in every FE's Catalog

## Example dialogue

> **Dev:** "So on the canvas I drag components anywhere, like Figma?"
> **Domain:** "No — you drop them into **Flow** containers. A floating nav isn't dragged to a coordinate; it's a Component whose own CSS uses `position: sticky`. You place it in the tree; it positions itself."
> **Dev:** "Then how does the agent get exact pixels?"
> **Domain:** "It doesn't want pixels. It wants the **Design Tree** — real component names, params, and CSS. A screenshot is a lossy bonus; the tree is the precise payload."

## Flagged ambiguities

- "design system" fused two wants: **(a)** defining tokens (color/type/spacing) and **(b)** composing screens. Resolved: MVP is **(b) composition only**; tokens deferred.
- "layout" was used for both the **Shell** (`@layout`) and CSS **Flow layout**. Resolved: "Shell" for the `@layout` backdrop, "Flow layout" for CSS arrangement.
- "figma clone / absolute canvas" implied x/y positioning. Resolved: **Flow layout** (real CSS incl. `position` as a property), not coordinate pinning.
