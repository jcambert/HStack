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
- US-035 Keep direct M2 agent launch and `hstack tmux` as explicit fallbacks.

## Done — M4 Network & Security

- US-040 Validate and propagate corporate proxy configuration without TLS bypasses.
- US-041 Protect secrets locally and scope release by project + agent + policy.
- US-042 Inspect declared/live workspace security and produce A/B/C/D/Critical score.
- US-043 Diagnose host/project network, certificates, agents, sessions and security.

## Ready next — M5 Token Efficiency

- EPIC-014: token optimizer abstraction, RTK integration, policies, metrics and doctor support.

## Later

- OpenViking shared context — M6.
- Optional sandbox-provider spikes remain deferred until the native backend is stable.
