# Transitions are exported codegen data, authored in a Flow graph, not a live prototype

**Status:** accepted

Plaxtar now models **Transitions** — a component node's event (e.g. `UIButton.OnClick`) directed at a target State (`modal-open`) or another Screen (`audit-detail.default`). We decided a Transition is **authored + exported only**: the Composer writes it to JSON and the *agent* generates the real wiring (a visibility flag / overlay reveal for a same-Screen target; `NavigationManager` to the target Screen's `route` for a cross-Screen target). The Composer never executes transitions — no play mode, no event interception, no navigation stack. This keeps Plaxtar a **codegen input**, not a prototyping tool (Framer/ProtoPie), consistent with ADR 0002 and 0003 (the tree is authoritative for identity/params/nesting; the agent owns logic and pixels).

## Considered options

- **Author + export only (chosen).** Draw edges, write JSON, agent codegens. Smallest runtime, ships within the existing export pipeline.
- **Simulate live, then export.** Rejected: a play-mode runtime that intercepts real component events and swaps States is a large surface that drifts Plaxtar toward being a prototyping tool with its own behavior semantics to reconcile against the generated code.
- **Nothing new / handler-name strings only.** Rejected: the agent can't reliably infer which button opens which State from a bare `"OnClick":"Open"` string.

## Storage and shape (the non-obvious parts)

- An edge lives **on the triggering event**, in the source State's own Design Tree — *not* in a central edge list. Node Ids are **regenerated per file** (`NewState`/clone mint fresh Ids), so any cross-file reference keyed by node Id would rot instantly. Targets are therefore **State/Screen names** (stable), never Ids.
- The `events` value is **normalized to an object** `{ handler?, to? }` — the previous bare-string form (`"OnClick":"Open"`) is dropped and existing design files + the CLAUDE.md codegen contract are migrated. Chosen over a string|object union to keep the decoder and codegen single-shape.
- Target grammar: **bare** name = sibling State of this Screen; **dotted** `screen.state` = another Screen (agent navigates to its `route`).
- Only **component** nodes expose ports (their `EventCallback` params, reflected from the Catalog). Raw elements (`<button>`, `<a>`) have no event model yet, so element-triggered transitions are a deliberate follow-up, not in this cut.

## Consequences

- A new **Flow view** editor (States as boxes, event ports, drag-to-connect arrows) is added to the Composer. Its box positions are editor-only metadata in a `designs/<screen>.flow.json` sidecar (`plaxtar.flow/v1`) — committed so teams share a layout, but **never** in the Design Tree (which stays coordinate-free per ADR 0002) and **never** read by codegen.
- The agent-facing codegen contract must now document how to realize both transition kinds; this ships as a canonical `docs/plaxtar-codegen.md` that the Catalog export also copies next to `designs/` for consumer repos.
