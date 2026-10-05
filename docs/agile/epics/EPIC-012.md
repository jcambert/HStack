# EPIC-012 — Security Policies & Inspection

**Status:** Done  
**Milestone:** M4  
**Priority:** Critical

## Goal

Make the effective workspace security posture inspectable and fail visibly.

## Delivered

- `hstack security inspect <project>`.
- Declared-plan fallback plus live Docker inspection when the container exists.
- Host/writable mounts, ports, environment names, secret names, user, capabilities, security options, networks, privilege, Docker socket, devices, PID and IPC modes.
- Security score A/B/C/D/Critical.
- docker.sock, privileged, host network and host-root mount force Critical.
