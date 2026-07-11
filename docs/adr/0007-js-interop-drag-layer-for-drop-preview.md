# A JS-interop drag layer for live drop preview

## Status

accepted

## Decision

Drag-and-drop hover feedback is handled in a **client-side JS module**, not in the Blazor circuit. While a drag is in progress, JS owns the hover math and the visual: for the node under the cursor it resolves one of three **drop zones** — top-half = **Before**, bottom-half = **After**, middle (containers only) = **Into** — and draws a cursor-following insertion line (or highlights the container for Into). `.NET` is invoked **exactly once, on drop**, with `(targetId, position)`.

The Composer renders the hooks JS needs onto each node: `data-node-id` and `data-container`. JS resolves validity itself (a drop onto the dragged node's own DOM subtree is rejected via DOM ancestry, since the canvas DOM mirrors the tree), so no per-move server round-trip is needed to know what's a legal target. `dataTransfer` carries either an existing `node-id` (reorder/move) or a new-node spec (component name / element tag / text), so the same drop handler serves reorder and insert-from-panel.

On drop, JS calls one of:

- `MoveNodeTo(dragId, targetId, pos)` — reorder/reparent an existing node,
- `DropNewAt(targetId, pos, payload)` — insert a new node,

where `pos ∈ { Before, Into, After }` and `Into` is honored only when the target is a container. These replace the coarse `MoveNode` / `DropNew` / `DropOn` (which appended to a container's end or dropped after a leaf, with no before/after and no feedback).

The same insertion-line model runs in the **Layers tree**, giving a precise reparent / multi-level move-out path that sidesteps canvas occlusion (see [0006](0006-edit-mode-floating-panels-honest-width-vs-occlusion.md)).

## Context

The canvas is authored by the page as a `RenderFragment` with page-bound handlers (the interactivity note at `Composer.razor:597`). `ondragover` was left deliberately **handler-less** — `preventDefault`-only — because a handler firing on every mousemove would push a render per mousemove through the Server circuit, spamming it with latency and churn. The cost: drops were **blind**. You could not see whether a drag would swap two siblings, land between them, or pull a node out of its container — the exact pains this decision addresses.

A live, cursor-following insertion line fundamentally needs per-move feedback. Options considered:

- **Pure-Blazor container highlight (dragenter/leave only)** — cheap, few events, but no before/after line: you still can't see *where between siblings* a drop lands. Doesn't solve swap/insert-between.
- **Pure-Blazor explicit dropzone slots** — a thin dropzone element between every sibling + an Into zone per container, each lit on dragenter/leave. Gives before/after precision with no JS, but multiplies DOM, feels jumpy, and edge-zones for move-out crowd out when containers are packed.
- **JS-interop drag layer (chosen)** — move all per-move work off the circuit. Smooth 60fps preview, one `.NET` call on drop, and it's a superset that also powers the Layers-tree drag.

This reverses the project's prior lean toward keeping the RCL JS-free. It is also the realization of the previously-parked "#18 JS-interop drag layer" slice.

## Consequences

- The `Plaxtar.Designer` RCL now ships a JS module (drag hit-testing + insertion-line rendering) loaded by the Composer. The JS-free property of the package is knowingly given up for this feature.
- Drop positioning becomes precise (Before / Into / After) across both the canvas and the Layers tree, via a single `MoveNodeTo` / `DropNewAt` API. No schema change — drop position is a transient editing action; only the resulting child order is persisted.
- JS and the render must agree on the contract (`data-node-id`, `data-container`, and the `dataTransfer` payload shape). If the canvas render stops emitting those hooks, drag silently breaks — they are load-bearing, like the codegen-sync constraint on the two render paths in 0005.
- Self-descendant rejection and target legality live in JS (DOM-ancestry based). The `.NET` side must still defensively re-validate on drop, since the JS is now the first — but not the only — line of correctness.
- Keyboard-accessible DnD is not provided by HTML5 drag; it stays deferred (acceptable for a dev-only tool).
