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

## Agent state

M2 adds dedicated per-project state binds for Claude Code, Codex, Hermes Agent and OpenCode. The project definition defaults to `stateScope: isolated`; any other state scope fails closed in M2.

HermesStack does not automatically mount host `~/.claude`, `~/.codex`, `~/.hermes`, OpenCode credentials, `.ssh`, cloud credential directories or Docker endpoints. Authentication is performed from inside the selected workspace and persists into that project's dedicated state directory.

Codex uses a project `CODEX_HOME` with file credential storage. Claude uses a project `CLAUDE_CONFIG_DIR`. Hermes uses a project `HERMES_HOME` and is forced to the local terminal backend. OpenCode's config/data homes are project mounts and automatic updating is disabled in the managed image runtime.

`HostMountValidator` classifies candidate project paths before a deployment plan is created. Compose generation performs a second fail-closed check for Docker daemon exposure and mandatory security flags.


## M3 session isolation

Herdr runs only inside the selected project workspace. `HERDR_SESSION=hstack-<project>` is injected by the validated deployment plan, and Herdr's config/session snapshots persist under that project's mounted `/home/hstack`.

The Herdr headless server is launched through structured `docker compose exec -d`; no shell is introduced and no Docker endpoint is mounted into the workspace. Official Herdr agent integrations write only to the already project-scoped Claude, Codex, Hermes and OpenCode state directories.

tmux is a fallback inside the same sandbox. It does not widen host mounts, privileges, namespaces or network policy.
