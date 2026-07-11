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

**State**:
A named variant of a Screen with specific conditions applied (default, modal-open, error, empty). Each State exports as its own Design Tree.
_Avoid_: variant, mode

**Screen**:
The unit of design in the MVP: one page's content region plus its States, rendered inside a chosen Shell.
_Avoid_: page (page = the eventual real `.razor`; Screen = its design)

**Shell**:
The real Blazor layout (`@layout` / `MainLayout` with `@Body`) a Screen is designed inside. Rendered live as a fixed backdrop; not edited.
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
- Each **State** exports one **Design Tree** (JSON) + optional screenshot into the repo
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
