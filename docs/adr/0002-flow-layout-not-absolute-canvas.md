# Flow/DOM layout, not an absolute (Figma-style) canvas

## Status

accepted

## Decision

The Composer arranges components with **real CSS flow** — flex/grid/stack containers, widths, and `position` (`sticky`/`fixed`/`absolute`) as a per-node CSS property scoped to its container. It does **not** offer a Figma-style canvas where every element is pinned to raw x/y coordinates.

## Context

An absolute canvas *feels* like Figma and is the intuitive "design tool" default, but its export stores coordinates, not structure. An agent turning coordinates into real Blazor markup must reverse-engineer the flex/grid that produces those pixels — it guesses, and drifts on spacing, nesting, and resize. That reverse-engineering is exactly why Figma→code drifts. Flow layout instead stores the real container hierarchy, which maps 1:1 to `.razor`. It is also *less* work: the canvas renders real components in a real browser that already lays them out by flow, so forcing absolute positioning would fight the runtime and discard the components' real responsive behavior.

## Consequences

- Dragging is "drop into a container/slot," not "drop at a coordinate" — more structured, less freeform. This is intentional; the structure is what makes the export trustworthy.
- Floating UI (nav, tooltips, overlays) is still supported via `position` as a CSS property, exactly as real code does it.
- Pure pixel-art / marketing one-offs are out of scope; this tool targets a component-driven design system feeding an agent.
