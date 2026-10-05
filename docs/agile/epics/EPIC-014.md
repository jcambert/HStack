# EPIC-014 — Token Optimization

**Status:** Done  
**Milestone:** M5  
**Priority:** Medium

## Goal

Reduce avoidable agent-context token load without weakening security, hiding critical diagnostics, or presenting estimates as billing savings.

## Existing Solutions Review

### RTK

- Existing upstream capability: yes; RTK owns shell-command rewriting and output compaction.
- Decision: integrate, do not reimplement.
- Pin: v0.51.0 official release artifacts with published SHA-256 digests.
- Managed agents: Claude Code, Codex, Hermes Agent and OpenCode.
- Metrics: RTK gain is shell-output reduction converted to estimated tokens; it is not provider billing.
- Security impact: RTK upstream local tracking can retain command strings and recall can retain raw output. HermesStack therefore forces the tracking DB to workspace tmpfs, disables recall, disables telemetry, and persists only sanitized aggregates.
- Exit strategy: disable/uninstall RTK integrations and remove the provider selection without changing agent state ownership.

### Caveman

- Existing upstream capability: yes; Caveman supplies native agent skill/plugin integration.
- Decision: integrate the signed v2.7.0 release source; do not implement semantic response compression.
- Pin: v2.7.0 signed tag, commit `8b0c1d3699b8d83e87fe4605b378da20c41555e0`.
- Managed agents: Claude Code, Codex, Hermes Agent and OpenCode.
- Security/quality impact: semantic compression may be lossy, so it is opt-in, restricted to aggressive/custom profiles, and never stacked with RTK without explicit consent.
- Exit strategy: invoke the upstream uninstaller inside the project-isolated workspace.

## PBIs

- PBI-1401 Token optimizer abstraction.
- PBI-1402 RTK integration.
- PBI-1403 Caveman integration.
- PBI-1404 Token optimization policies.
- PBI-1405 Token metrics and gain reporting.
- PBI-1406 Token optimization diagnostics.
