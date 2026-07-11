# Edit-mode floating panels: honest canvas width at the cost of edge occlusion

## Status

accepted — amends [0005](0005-canvas-fidelity-raw-preview-vs-wrapped-edit.md) (the Edit-mode *width* consequence only)

## Decision

Edit mode stops subtracting width from the canvas. The rail + active panel (left) and the Properties pane (right) become **floating overlays** (`position: fixed`) drawn *on top of* a canvas that always renders at **true full app width**. The panels occlude the outer left/right edges of the design; that occlusion is the accepted cost.

This changes only the **width** half of 0005. The **wrapper drift** half is unchanged: Edit still wraps nodes in `pd-node` for click-select, so **Preview remains the structural truth**. Edit now matches production on *width*, Preview matches on *width + DOM structure*.

Occluded nodes stay fully editable via three paths:

- **Collapse the panel** — reclick the active rail icon; the panel hides, the whole canvas is revealed, click the node, reopen.
- **Layers tree** — clicking a node in the Layers panel selects it even while it's hidden behind a panel on the canvas.
- **Canvas right-click context menu** — Insert (searchable flyout of Components/Elements/Text, inserts into the right-clicked container or as a sibling on a leaf) · Delete · Duplicate · Add as overlay. Lets a dev keep working with panels collapsed.

Preview mode is unchanged except that its top bar (Edit/Save) **auto-hides after 3s idle and reveals on top-edge hover**, for full-immersion true-fidelity viewing. The Edit top bar stays pinned.

## Context

0005 chose "split by mode": Edit keeps a width-subtracting grid (narrow canvas accepted), Preview is the full-width escape hatch. 0005 **explicitly rejected** floating panels — "fixes width but the panels occlude the design's edges, and it does nothing for the wrapper drift."

Field use reversed the width call. A narrow Edit canvas does not just look wrong — it **biases the designer**. Every flex/grid/percentage judgment is made against a false width during the *primary* editing activity, producing "the canvas feels tight" decisions that would not be made at real width (a hallucination risk for both the human and the agent consuming the export). That silent, always-on distortion is worse than occlusion, which is **visible and dismissable** (collapse a panel, or jump to Preview).

The two 0005 objections, re-weighed:

- *"Panels occlude the edges"* — still true, but now mitigated by affordances that did not exist when 0005 was written: a Layers panel (selection proxy) and a canvas context menu (edit without the panel open). Occlusion is no longer a dead end.
- *"Does nothing for wrapper drift"* — correct and accepted. This decision is **orthogonal** to wrapper drift. Preview already owns structural fidelity; we are only reclaiming *width* fidelity for Edit, not claiming Edit is now byte-identical to production.

Responsive/breakpoint fidelity remains out of scope (still an iframe follow-up, per 0005).

## Consequences

- Edit canvas renders at true full width always; the "narrow content region" consequence of 0005 is **retired**. 0005's wrapper-drift consequence and "Preview is the artifact to trust" stand.
- Panels are `position: fixed` and no longer participate in layout flow; toggling a panel never reflows the canvas (width stays stable).
- Design edges can sit behind panels. Editing an occluded node relies on collapse / Layers / context menu — these become load-bearing, not nice-to-haves.
- A new interaction surface (canvas right-click context menu) must stay in sync with the same node operations as the panels (insert/delete/duplicate/overlay) and the codegen contract.
- Preview gains an auto-hide timer + top-edge hover zone; the only way back to Edit while the bar is hidden is the hover reveal, so that hit zone must be reliable.

### Update (panel-toggle rail relocated)

The always-visible icon rail described above originally floated on the canvas — but the canvas renders the app's real Shell, whose own left nav the rail landed on top of. The rail's panel-toggle icons (Layers/Components/Elements/Properties) therefore moved into the **top bar** (a group next to the screen/state selectors); there is no floating rail on the canvas anymore. The **panels themselves still float** over the full-width canvas — the width-honesty decision above is unchanged. Layers/Components/Elements remain mutually exclusive (left); Properties is an independent toggle (right); both a left panel and Properties can be open at once. `Ctrl+B` hides/restores all panels for a full-canvas Edit view (distinct from Preview's raw render).
