# EPIC-011 — Secrets & Authentication

**Status:** Done  
**Milestone:** M4  
**Priority:** Critical

## Goal

Keep secret values out of configuration, Compose and diagnostics while allowing explicit project/agent-scoped use.

## Delivered

- `ISecretStore` and `LocalProtectedSecretStore`.
- Windows DPAPI current-user protection.
- AES-256-GCM protection outside Windows with a local 0600 master key.
- YAML contains policy metadata only: project, secret name and allowed agents.
- Values enter through host environment and are injected only into the selected agent exec process.
- Secret redaction service and non-leak tests.
