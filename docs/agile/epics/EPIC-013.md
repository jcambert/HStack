# EPIC-013 — Doctor & Diagnostics

**Status:** Done  
**Milestone:** M4  
**Priority:** High

## Goal

Provide actionable host, network, certificate, workspace, agent and security diagnostics.

## Delivered

- `hstack doctor` and `hstack doctor <project>`.
- Targeted `--network`, `--certificates` and `--security` modes.
- Docker/Compose, project, DNS, HTTPS/TLS, proxy, CA, agent, Herdr/tmux and security checks.
- Non-zero exit status when a diagnostic fails.
