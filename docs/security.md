# Security posture

The workspace container is a blast-radius reduction boundary, not a VM-grade security boundary.

Mandatory workspace invariants:

- no Docker daemon socket or Windows Docker engine pipe;
- no privileged containers;
- no host network/PID/IPC namespaces;
- `no-new-privileges:true`;
- all Linux capabilities dropped by default;
- only the exact project path is mounted read-write;
- host HOME and sensitive credential directories are forbidden;
- published ports bind to `127.0.0.1` by default;
- workspace root filesystem is read-only; only explicit project/home/tmp paths are writable;
- workspace processes run as non-root user `hstack`;
- corporate CA trust is added without disabling TLS verification.

## Agent and session state

Agent authentication/state is isolated per project. Codex uses a project `CODEX_HOME`, Claude a project `CLAUDE_CONFIG_DIR`, Hermes a project `HERMES_HOME`, and OpenCode project config/data mounts.

Herdr runs only inside the selected workspace. `HERDR_SESSION=hstack-<project>` and Herdr snapshots persist under that project's HOME. tmux is a fallback inside the same sandbox.

## M4 corporate network policy

Proxy configuration supports `HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY` and lowercase variants. Mandatory exclusions include localhost, loopback and `host.docker.internal`. Inline proxy credentials are rejected so credentials cannot enter `hstack.yaml`.

Corporate CA support remains additive. HermesStack never uses `curl -k`, `NODE_TLS_REJECT_UNAUTHORIZED=0`, `GIT_SSL_NO_VERIFY=true` or equivalent TLS bypasses.

## M4 protected secrets

Secret values never belong in `hstack.yaml`, `projects.yaml` or Compose.

`LocalProtectedSecretStore` uses Windows DPAPI current-user protection on Windows. On Unix-like hosts it uses AES-256-GCM and a locally generated master key restricted to the current user. `secrets.yaml` stores policy metadata only.

A secret is resolved only when project + agent + explicit policy match. Direct agent launch passes the value to the selected `docker compose exec` process environment; the long-lived workspace service environment does not contain the secret.

`ISecretRedactor` masks known values and common token shapes before diagnostic error output.

## M4 inspection

`hstack security inspect <project>` reads the validated deployment plan and, when available, the live Docker container. It reports mounts, writable mounts, ports, environment names, secret names, user, capabilities, security options, networks, privilege, Docker socket, devices, PID and IPC modes.

The score is A/B/C/D/Critical. Docker socket exposure, privileged mode, host networking or a host-root mount always produce Critical.
