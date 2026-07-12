# Simulate walks authored States; it is not a prototyping runtime

**Status:** accepted

Plaxtar adds a **Simulate** (Play) mode: a third Canvas mode where clicking a wired control loads that Transition's target **State**, so you can walk a flow and *feel* it. We deliberately scoped it as **verification, not prototyping** — it invents no behavior (no animation, gestures, form logic, or fake data); it only navigates between States you already authored, each of which is a real render. The goal is to catch a wrong flow *before* spending codegen tokens on it, and it stays consistent with ADR 0008 (Transitions are authored data): Simulate "runs" a transition only by loading the authored target State, never by executing invented logic.

## Considered options

- **Bounded Simulate that walks authored States (chosen).** Reuses the Preview raw render + the States on disk. Small, on-mission, gives ~80% of the "feel the flow" value.
- **Full prototyping pivot.** Rejected: competes with Figma/ProtoPie/Framer, reintroduces design→code drift (a second source of truth that lies about the app), and dilutes the codegen wedge. Off-mission.
- **Stay codegen-only, no Simulate.** Viable, but the flow graph alone doesn't let a dev *feel* a flow; a thin verification layer measurably reduces wrong-direction codegen (token burn).

## Key decisions (the non-obvious parts)

- **Throwaway sim session.** Simulate renders a separate, disposable `DesignSession` loaded from the saved files; the real editing Session (tree, selection, undo history) is never touched. `LoadAsync` is destructive, so driving the real Session and restoring it was rejected as fragile.
- **Reads saved files, with a dirty-guard.** Because it renders from disk, unsaved edits to the current State won't appear (they are *not* lost). Entering Simulate while dirty prompts: Cancel / Save & simulate / Simulate anyway.
- **Uniform "load whole target State" nav, not overlay-toggle.** Every transition — same-screen or cross-screen — just loads `(screen, state)` and re-renders. The target file already contains exactly what was authored for that State (base + overlay). Rejected mimicking the runtime (keep base, reveal only the overlay): it needs state-diffing, a second code path, and breaks when a same-screen State rewrites the base. Cost: live component state (e.g. typed text) resets on a jump — acceptable for a verification tool. A smoother overlay-toggle is a possible later refinement.
- **Click-catcher wrapper, not real EventCallback injection.** Wired nodes are wrapped in a `display:contents` element with `@onclick`. Near-zero layout impact, works for every wired node, cheap. Rejected reflecting over `EventCallback<T>` per component (faithful to the real event but heavier); approximate "click the control's area to navigate" is enough to feel a flow. Only component-wired nodes participate (elements have no event ports yet).

## Consequences

- A Simulate HUD carries: current `screen.state` indicator, Back (visited-state stack), Reset to entry State, Exit, and a Show-hotspots toggle (default on) that outlines all wired controls. Broken targets (missing file) get a distinct outline and a HUD notice instead of navigating.
- Simulate is flow-fidelity, not pixel-fidelity — that remains Preview's job. The two modes stay distinct (Preview = look, Simulate = walk).
