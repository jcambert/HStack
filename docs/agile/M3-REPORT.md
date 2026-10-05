# M3 Increment Report — Sessions

## Scope

M3 completes EPIC-009 with Herdr as the project session manager and tmux retained as a fallback multiplexer.

## Pinned toolchain

- Herdr: **0.9.3**, immutable GitHub release assets with SHA-256 verification for Linux x86_64 and arm64.
- tmux: installed from the pinned Debian base image.
- Workspace image: **0.3.0**.

## Session model

Every HermesStack project uses:

- Herdr session: `hstack-<project-id>`
- Herdr workspace label: `hstack:<project-id>`
- cwd: `/workspace`
- persistent Herdr state: project-owned `/home/hstack/.config/herdr`

`HerdrSessionService` starts the Herdr headless server through detached orchestrator execution, installs the official integrations for Claude Code, Codex, Hermes Agent and OpenCode, reuses a restored project workspace when present, and creates one only when absent.

## CLI

```text
hstack session init <project>
hstack session status <project>
hstack session list <project>
hstack session agents <project>
hstack session run <agent> <project> --name <name> [-- <agent args>]
hstack session stop <project>
hstack herdr <project>
hstack tmux <project> [session-name]
```

Direct M2 agent commands remain supported as the fallback path.

## Persistence behavior

Client detach is handled natively by Herdr: the background server and pane processes continue running. When the Docker workspace is recreated, the server process is new but Herdr reloads its persisted layout. Current official Herdr integrations allow supported agents to report native session ids so Herdr can resume their conversations after a server restart.

## Validation

The M3 gate retains all M1/M2 security and agent assertions, verifies the pinned Herdr/tmux toolchain, installs all four Herdr integrations, proves the Herdr snapshot is written inside ProjectA state, recreates the Docker workspace, proves the same logical project workspace is restored without duplication, and exercises a detached tmux fallback session.

Interactive third-party account login remains intentionally outside CI because CI must not contain real agent credentials.
