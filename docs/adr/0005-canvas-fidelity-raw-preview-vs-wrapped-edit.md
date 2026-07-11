# Canvas fidelity: a raw Preview mode, distinct from wrapped Edit mode

## Status

accepted

## Decision

The Canvas has **two render modes**, and *fidelity is defined by Preview, not Edit*:

- **Edit mode** (default) wraps every node in a `<div class="pd-node">` selection box (for click-select, hover outline, and the node-type tag), and shows the palette/props side panels. The content region is therefore narrower than production, and each node carries an extra wrapper element.
- **Preview mode** collapses the side panels so the content region gets its real desktop width, and renders the Design Tree **raw** — no wrapper, no tag, no empty-slot placeholder, no selection `onclick`. The Shell (`LayoutView`) still renders in both modes. Preview is non-interactive for *design-selection*; the live components themselves keep working.

"Same as result" means **Preview mode**. Preview renders the exact DOM the generated `.razor` would produce.

## Context

The whole point of the Composer (vs Figma) is *no drift* — what you compose is what ships. Two things silently broke that in the Edit-mode canvas:

1. **Width.** The canvas is the middle `1fr` column between a 180px palette and a 240px props panel, and it renders the full Shell (sidebar) inside that column. So the designed `@Body` got `1fr − 180 − 240 − sidebar`, while production `@Body` gets `viewport − sidebar`. Percentage/flex/grid widths and any width-sensitive layout looked wrong.
2. **Structure (the deeper one).** Every node is wrapped in a `pd-node` div so it can be outlined and clicked. Production has no such wrapper. Inside a flex/grid parent, that wrapper — not the component — becomes the flex/grid item, so `width:100%`, `flex:1`, `gap`, `:first-child`, etc. resolve differently *regardless of width*.

Options considered:

- **Floating panels over a full-bleed canvas** — fixes width but the panels occlude the design's edges, and it does nothing for the wrapper drift.
- **`.pd-node { display: contents }` globally** — removes the wrapper box in both modes so the component is always the real flex/grid item, but `display:contents` leaves no box to outline or hit-test, so selection would need reworking (child outline / JS hit-testing). Riskier, and still can't simulate a different viewport.
- **Split by mode (chosen)** — keep the wrapper for selection while editing (cosmetic drift is acceptable there), and make Preview the raw, panel-collapsed, true-fidelity view. One `_preview` flag branches the canvas render; no new mechanism.

Device-width / responsive simulation (media queries firing at a simulated width) is explicitly **out of scope**: media queries evaluate against the browser viewport, and only an **iframe** canvas gives the design its own viewport. That is a separate, larger slice; Preview targets *desktop* width fidelity, which the editor's own maximized viewport already provides.

## Consequences

- The Composer gains an Edit/Preview toggle. Preview is the artifact to trust when checking "does this match production."
- Edit mode keeps its cosmetic wrapper drift by design — it is the price of in-place click-select, and it never reaches the export or the generated page.
- Two render paths exist in the canvas (`RenderNode` branches on `_preview`). They must stay structurally in sync with the codegen contract (raw element → `<tag>`, component → `DynamicComponent`), or Preview stops being trustworthy.
- Responsive/breakpoint fidelity is knowingly deferred; if a real FE leans on breakpoints, an iframe-based device-preview is the follow-up (a superset of this decision, not a reversal).
