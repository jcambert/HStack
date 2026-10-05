# EPIC-009 — Herdr & tmux Session Management

**Status:** Done  
**Milestone:** M3  
**Priority:** High

## Goal

Provide persistent, resumable project sessions without moving agent execution outside the isolated workspace.

## Delivered

- Herdr 0.9.3 pinned and checksum-verified.
- One named Herdr session and logical workspace per HermesStack project.
- Official Claude, Codex, Hermes and OpenCode Herdr integrations.
- Named agent launch through `herdr agent start`.
- Persisted session layout across Docker workspace recreation.
- tmux fallback retained inside the workspace.
- Direct M2 agent launch retained when session management is not desired.
