# Per-FE install; Catalog scoped to the running process's assemblies

## Status

accepted

## Decision

The Composer is installed **per micro-frontend**, and its **Catalog** is the reflection over the assemblies loaded in *that FE's process* — i.e. `.Base.UI` (shared) plus the running module FE (e.g. `.UI.Audit`). It does not attempt to enumerate or render components from other FEs' processes.

## Context

Each FE is a separate deployable Blazor app = separate process = separate assembly set (`.UI` on `:45546`, `.UI.Audit` on `:44549`, …), stitched onto one domain by a proxy at the HTTP layer at runtime — not at the component/assembly layer. A process cannot reflect over or render another process's components. The team's workflow already runs only a subset (a module team runs `.UI` + their module, not LMS/KMS), so a per-process Catalog naturally yields exactly the components in scope: shared + the module being worked on. Attempting cross-FE composition would require sharing a runtime the micro-frontend architecture deliberately separates.

## Consequences

- Shared components (and ideally the **Shell**) should live in `.Base.UI` so they appear in every FE's Catalog and render in-process.
- Modules you don't run are absent from the Catalog — correct, not a bug.
- Designing a page inside *another* FE's Shell in-process requires that Shell to be referenced (again, put it in `.Base.UI`); otherwise design against the module's own layout or a stand-in.
