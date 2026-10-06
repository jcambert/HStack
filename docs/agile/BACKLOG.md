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

- US-030..035 Herdr sessions, official agent integrations, persistence and tmux fallback.

## Done — M4 Network & Security

- US-040 Validate and propagate corporate proxy configuration without TLS bypasses.
- US-041 Protect secrets locally and scope release by project + agent + policy.
- US-042 Inspect declared/live workspace security and produce A/B/C/D/Critical score.
- US-043 Diagnose host/project network, certificates, agents, sessions and security.

## Done — M5 Token Efficiency

- US-1401 Register token optimizers behind a common abstraction.
- US-1402 Enable RTK for Claude Code.
- US-1403 Enable RTK for Codex, Hermes and OpenCode.
- US-1404 Prevent unsafe optimizer stacking.
- US-1405 Display evidence-qualified token gains per project.
- US-1406 Diagnose token optimizer health and privacy posture.

## In Progress — M6 Shared Context

- PBI-1501..1509: OpenViking provider, project identities, shared ACL namespaces, agent integrations, budgets, secret filtering, observability and export/import.
- Release blockers: provider-native project isolation, no root/admin key in workspace, M6 CI gate.

## Later

- Operations — M7.
- Aspire Experience — M8.
- Optional sandbox-provider spikes remain deferred until the native backend is stable.
