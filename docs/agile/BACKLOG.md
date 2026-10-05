# Backlog

## Done — M1 Secure Foundation

- US-001 Add a local project securely.
- US-002 Start an isolated workspace with Docker Compose.
- US-003 Stop an isolated workspace.
- US-004 Enter the workspace shell.
- US-005 Report workspace status.
- US-006 Initialize HermesStack idempotently.
- US-007 Register integration descriptors without hard-coded compatibility tables.
- US-008 Add/import Corporate CA certificates through CLI.

## Done — M2 Agent Runtime

- US-010 Pin and install Claude Code using its current native installation path.
- US-011 Pin and install Codex with an exact supported package-manager version.
- US-012 Pin and install Hermes Agent from an exact stable release tag.
- US-013 Pin and install OpenCode with an exact supported package-manager version.
- US-014 Add `IAgentHarness`, harness registry and common agent launch path.
- US-015 Add project-scoped authentication/state for all four agents.
- US-016 Add ergonomic `hstack claude|codex|hermes|opencode` aliases without duplicated logic.
- US-017 Validate M2 in real Docker with exact versions and ProjectA/ProjectB state isolation.

## Ready next — M3 Sessions

- Integrate Herdr as the session manager.
- Add named/resumable sessions per agent and project.
- Keep direct launch fallback when Herdr is unavailable.
- Persist Herdr state per project without host credential reuse.

## Later

- Full user-facing `hstack security inspect` command — EPIC-012 / M4.
- SPIKE-2201 Evaluate Dagger Container Use provider — deferred until the native backend is stable.
- SPIKE-2202 Evaluate DevPod provider — deferred until the native backend is stable.
