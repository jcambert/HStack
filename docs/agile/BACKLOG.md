# Backlog

## Done — M1 Secure Foundation

- US-001 Add a local project securely.
- US-002 Start/stop and enter an isolated Docker workspace.
- US-003 Report workspace status and initialize HermesStack idempotently.
- US-004 Register integrations and Corporate CA trust without weakening TLS.

## Done — M2 Agent Runtime

- US-010..017 Pin and integrate Claude Code, Codex, Hermes Agent and OpenCode.
- Add `IAgentHarness`, project-scoped auth/state and real Docker isolation validation.

## Done — M3 Sessions

- US-030 Pin Herdr and keep tmux in the workspace toolchain.
- US-031 Create one deterministic Herdr session/workspace per HermesStack project.
- US-032 Install Herdr's official Claude, Codex, Hermes and OpenCode integrations.
- US-033 Launch named agents through Herdr panes without shell interpolation.
- US-034 Persist Herdr state in the project HOME and restore layout across workspace recreation.
- US-035 Keep direct M2 launch and `hstack tmux` as explicit fallbacks.

## Ready next — M4 Network & Security

- EPIC-010..013: network policy, security inspection, secrets and hardening diagnostics.

## Later

- RTK token optimization — M5.
- OpenViking shared context — M6.
- Optional sandbox-provider spikes remain deferred until the native backend is stable.
