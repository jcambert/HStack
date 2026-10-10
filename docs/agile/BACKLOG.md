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

## Done — M6 Shared Context

- PBI-1501..1509 completed: provider abstraction, pinned OpenViking deployment, project identities, restricted shared namespaces, first-party agent integrations, context budgets, secret filtering, observability, export/import and confirmed memory clear.
- Security completion: account ACL enforcement is fail-closed, private project credentials are isolated, shared writes require an explicit restricted namespace, and root/admin credentials never enter workspaces.

## Done — M7 Operations

- EPIC-016..020 completed: managed transactional updates, backup/restore and portability, redacted operational observability, automation-friendly CLI/dashboard, and CI/CD distribution.
- US-1601/1602, US-1701/1702, US-1801, US-1901 and US-2001 completed.
- Validation completed on Linux and Windows with M3-M7 regression gates green.
- Self-contained delivery artifacts published for win-x64, linux-x64 and linux-arm64 with SHA-256 manifests.

## Runtime-validated — M8 Aspire Experience

- EPIC-021 integrated in PR #12; runtime security, pinned Aspire CLI, full image, OpenViking and workspace workflows validated in PR #15 CI #136.
- Required M8 Linux smoke and Windows build/unit passed; multi-RID delivery artifact and post-merge main checks are still release blockers.

## Later

- Optional sandbox-provider spikes (M9) remain deferred until M8 release readiness is confirmed.
