# Plaxtar Designer

A dev-only, in-process visual **Composer** for Blazor. It assembles your **already-coded**
components into screens, renders them **live** inside your real app (real DI, auth, layout —
via `DynamicComponent`), and exports a codegen-grade **Design Tree** (`plaxtar.designer/v1`)
that an AI agent turns into real `.razor` pages.

The thing on the canvas *is* the real component, so "exact UI/UX" is literal, not a redrawn
approximation. No Figma redraw, no drift, no paywalled MCP — designs are plain JSON files in
your repo.

## Layout

| Path | What |
|------|------|
| `src/Plaxtar.Designer/` | The Composer, packaged as the `Plaxtar.Designer` RCL / NuGet. |
| `sample/SampleHost/` | Blazor Server dev harness (net10.0) to run the Composer. |
| `designs/` | Exported Design Trees + `_catalog.json` manifest. |
| `docs/` | [`plaxtar-codegen.md`](docs/plaxtar-codegen.md) (agent guide), [`adr/`](docs/adr/). |
| `SPEC.md` · `CONTEXT.md` | Spec + domain glossary. |

## Build the package (`.nupkg`)

The `.nupkg` is git-ignored — build it from source after cloning:

```powershell
# from repo root
dotnet pack src/Plaxtar.Designer/Plaxtar.Designer.csproj -c Release -o nupkg
```

Output: `nupkg/Plaxtar.Designer.<version>.nupkg`, multi-targeted `net8.0` + `net10.0`.
Requires the **.NET 10 SDK** to build (it multi-targets); the *consuming* app can stay
on .NET 8. Consume it by pointing a local feed at the `nupkg/` folder:

```xml
<!-- nuget.config -->
<add key="local" value="./nupkg" />
```

## Run the sample host

```powershell
dotnet run --project sample/SampleHost
```

Then open `/designer`.

## Install into your own app

See [`src/Plaxtar.Designer/README.md`](src/Plaxtar.Designer/README.md) — dev-only gating,
service registration, hosting the Composer at a route you choose, and prod-safety
verification. It ships as the NuGet package readme.

## Docs

- **[SPEC.md](SPEC.md)** — full specification (problem, workflow, schema, non-goals).
- **[CONTEXT.md](CONTEXT.md)** — domain glossary (Design Tree, Transition, Flow, Simulate, …).
- **[docs/plaxtar-codegen.md](docs/plaxtar-codegen.md)** — the agent's codegen contract; copied
  into consumer repos as `designs/AGENTS.md` on Catalog export.
- **[docs/adr/](docs/adr/)** — architecture decisions.
