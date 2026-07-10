# Design delivery is file-based; the MCP server reads the repo, not the running app

## Status

accepted · **amended** — MCP dropped from the MVP (see amendment below)

## Amendment (files-only, no MCP)

A filesystem-capable coding agent (Claude Code) can read the exported files directly, so the MCP server is redundant and is **removed from the MVP**. The contract is now: the agent reads `designs/*.json` + a `designs/_catalog.json` manifest, guided by the schema + codegen rules documented in the repo `CLAUDE.md`. MCP remains a possible future addition only for non-filesystem/remote/sandboxed agents. The rest of this ADR (file-based export, git-versioned, decoupled from the running app) stands.

## Decision

Exports are written as JSON files (+ optional PNG) under `designs/` in the FE repo and committed to git. The MCP server reads those files. The agent never connects to the running dev app; there is no live MCP-into-the-app channel in the MVP.

## Context

The alternative — a live MCP endpoint that pulls the current canvas from the running app — is always fresh but adds moving parts: the app must be running, an endpoint exposed, and the agent coupled to a live process. File-based export decouples design-time from consume-time: designs are versioned in git, reviewable in PRs, and readable when the app is down. It also keeps the MCP server tiny (a file reader) and the security surface minimal (no endpoint on the dev app).

## Consequences

- Designs can go stale relative to an in-progress canvas until the user hits Export — acceptable; Export is an explicit "save" action.
- Design history lives in git alongside the code the agent generates.
- A live-pull channel can be added later without changing the file format.
