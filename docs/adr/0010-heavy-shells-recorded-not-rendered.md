# Heavy app-shells are recorded for codegen but not live-rendered

## Status

accepted — refines the **Shell** term (CONTEXT.md) and the live-backdrop assumption behind [0006](0006-edit-mode-floating-panels-honest-width-vs-occlusion.md)

## Context

The Composer designs a Screen *inside* its Shell (the app's `@layout`). The original
assumption (ADR 0001, 0006): a Shell is a `LayoutComponentBase` and renders **live** on
the canvas as a fixed backdrop, so the design sits in the real layout at true fidelity.

That holds for clean shells (the sample host). It breaks on a real enterprise FE. Len's
`MainLayout`:

- **Does not inherit `LayoutComponentBase`.** It uses a custom base (`CommonPage`) and
  hand-rolls its own `[Parameter] RenderFragment Body`. So the reflection-based Shell scan
  (looking for `LayoutComponentBase`) missed it entirely — it fell through and appeared in
  the *component* palette instead of the Shell picker.
- **Is full app-chrome, not a passive frame.** `navbar … fixed-top` + `sidebar` are
  `position:fixed` with high z-index — they escape any canvas container and paint over the
  Composer's own panels/top bar. It also wraps `@Body` in `<AuthorizeView>` and fires
  service/API calls in `OnInitializedAsync`/`OnAfterRenderAsync`.

Live-rendering such a shell inside the design canvas is actively hostile: the fixed chrome
occludes the Composer UI, and running the layout's auth/lifecycle code outside a real
request context throws and drops the Blazor Server circuit (observed as a "random crash").

## Decision

Split **detecting** a Shell from **rendering** it.

- **Detect widely.** A Shell is anything `LayoutView` can host: a `LayoutComponentBase`,
  *or* any component exposing a public `[Parameter] RenderFragment Body` (the actual
  contract `LayoutView` needs). This surfaces custom-base layouts like `CommonPage` in the
  Shell picker. Detected shells are excluded from the component palette (a layout is not a
  draggable content component).
- **Render narrowly.** Only live-render a Shell that is a `LayoutComponentBase` (known-safe
  lifecycle). A heavy shell (Body-convention, custom base) is **recorded** on the Screen —
  so the exported tree still carries `shell` and the generated page gets `@layout
  MainLayout` — but the canvas stays **blank** (design the content region only). A
  `metadata only` badge next to the Shell picker signals this.

Reflection over host assemblies is also hardened: a single unresolvable type (routine with
DevExpress-scale dependencies) is skipped per-type instead of aborting the whole assembly
scan, which had silently emptied the Shell list.

## Consequences

- **Correct codegen without live fidelity.** The Screen records the real Shell name; the
  agent emits the right `@layout`. Visual fidelity of the *chrome* is lost for heavy shells
  — you design the content region on a clean canvas — but the chrome was never the thing
  being designed. Content-region flow/params keep full fidelity.
- **No overlap, no shell-induced crash.** The Composer UI is never occluded by host chrome,
  and the host layout's auth/service lifecycle never runs out of context.
- **A gap for teams who want chrome fidelity.** If someone genuinely needs the real chrome
  on the canvas, the path is a lightweight design-time layout (a `LayoutComponentBase` stub
  with just the content frame, no fixed chrome/auth) selected as the Shell — not rendering
  the production app-shell. Deferred; not built for the MVP.
- **`LayoutView` still owns the render.** Nothing changes for standard shells; the gate is
  a single `LayoutComponentBase.IsAssignableFrom` check on the resolved type.
