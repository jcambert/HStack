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
- M1 security acceptance: ProjectA/ProjectB filesystem isolation, non-root user, read-only root, dropped capabilities, no-new-privileges and no Docker socket are validated by the end-to-end gate.

## Ready next — M2 Agent Runtime

- US-010 Pin and install Claude Code using the current official installation mechanism.
- US-011 Pin and install Codex using the current official installation mechanism.
- US-012 Pin and install Hermes Agent using the current official installation mechanism.
- US-013 Pin and install OpenCode using the current official installation mechanism.
- Add `IAgentHarness`, harness registry and project-scoped authentication state.

## Later

- US-009 Full user-facing `hstack security inspect` command — EPIC-012 / M4.
- SPIKE-2201 Evaluate Dagger Container Use provider — deferred until the native backend is stable.
- SPIKE-2202 Evaluate DevPod provider — deferred until the native backend is stable.
