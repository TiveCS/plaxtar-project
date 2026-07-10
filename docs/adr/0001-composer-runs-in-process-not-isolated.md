# Composer runs in-process inside the real app, not as an isolated canvas

## Status

accepted

## Decision

The Composer ships as a NuGet package (`Plaxtar.Designer`) that adds a dev-only route (`/design`) **inside the target Blazor app**, rendering real components via `DynamicComponent` in the app's own process. It is **not** a standalone canvas app that references only the component assembly.

## Context

The obvious design is a separate canvas app that imports the component library and renders it in isolation — clean separation, reusable, no coupling to any one app. We rejected it: the target office app's components and Shell need live **auth, API, and DB just to render**, not just to function. An isolated canvas would have to mock a large, fragile service surface, and would still drift from real behavior. Running in-process means DI, auth, backend, and the real Shell are already wired — components that fetch on init "just work" against the dev backend, with zero shims.

## Consequences

- The Composer code is compiled into the app; it must be **dev-only** (build flag / excluded from prod).
- The tool is reusable across apps only to the extent it's packaged as a drop-in NuGet, not a shared running service.
- Development happens against a small sample host app in the Plaxtar repo; real validation happens by installing the package into the office app.
- Exports (Design Tree JSON) are written to the repo and read by the MCP server out-of-band, so the agent never needs the app running.
