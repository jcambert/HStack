# EPIC-010 — Corporate Network, CA & Proxy

**Status:** Done  
**Milestone:** M4  
**Priority:** High

## Goal

Make managed workspaces usable on corporate networks without weakening TLS.

## Delivered

- Existing corporate CA bundle support retained.
- Validated HTTP/HTTPS proxy configuration.
- Mandatory NO_PROXY merge for localhost, loopback and host.docker.internal.
- Uppercase and lowercase proxy variables injected into workspace plans.
- Proxy credentials in YAML are rejected.
- Targeted network/certificate diagnostics in `hstack doctor`.
