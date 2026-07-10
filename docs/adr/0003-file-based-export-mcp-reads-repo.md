# Design delivery is file-based; the MCP server reads the repo, not the running app

## Status

accepted

## Decision

Exports are written as JSON files (+ optional PNG) under `designs/` in the FE repo and committed to git. The MCP server reads those files. The agent never connects to the running dev app; there is no live MCP-into-the-app channel in the MVP.

## Context

The alternative — a live MCP endpoint that pulls the current canvas from the running app — is always fresh but adds moving parts: the app must be running, an endpoint exposed, and the agent coupled to a live process. File-based export decouples design-time from consume-time: designs are versioned in git, reviewable in PRs, and readable when the app is down. It also keeps the MCP server tiny (a file reader) and the security surface minimal (no endpoint on the dev app).

## Consequences

- Designs can go stale relative to an in-progress canvas until the user hits Export — acceptable; Export is an explicit "save" action.
- Design history lives in git alongside the code the agent generates.
- A live-pull channel can be added later without changing the file format.
