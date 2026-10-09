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

Agent authentication/state is isolated per project. Herdr runs only inside the selected workspace and persists under that project's HOME. tmux remains a fallback inside the same sandbox.

## Corporate network and secrets

Proxy configuration supports uppercase/lowercase HTTP(S)_PROXY and NO_PROXY variants. Inline proxy credentials are rejected. Corporate CA support remains additive.

Secret values never belong in `hstack.yaml`, `projects.yaml` or Compose. Protected values are released only when project + agent + explicit allow-list policy match.

## M5 token optimization

RTK v0.51.0 is installed from a release artifact whose SHA-256 is pinned in `toolchain.lock.yaml`. Caveman v2.7.0 source is pinned to its signed release commit.

RTK is the default optimizer for safe/balanced profiles. Caveman is semantic compression and is restricted to aggressive/custom profiles. RTK + Caveman is PotentiallyLossy and requires explicit consent.

The RTK upstream release can record command strings in its tracking database and can retain raw command output for recall. HermesStack mitigates this by:

- setting `RTK_DB_PATH=/tmp/hstack-rtk-tracking.db` so tracking is ephemeral on the workspace tmpfs;
- disabling RTK recall during provider setup;
- setting `RTK_RECALL=0`;
- setting `RTK_TELEMETRY_DISABLED=1`;
- never copying RTK's raw history into HermesStack durable state;
- persisting only aggregate token-gain records.

`hstack token gain` classifies RTK data as Estimated, not Measured. It does not infer provider cost savings.

`hstack security inspect` reports configured optimizer hooks, proxy endpoints and the prompt/content logging posture.

## M8 Aspire security and validation exception

The Aspire workspace must preserve all mandatory invariants above, including a read-only root filesystem, non-root execution, dropped capabilities, no privileged mode, no Docker socket, and loopback-only published ports. Do not weaken those controls to make Aspire's automatic certificate injection succeed. HStack-managed CA trust must remain additive; TLS verification must not be disabled.

The M8 Aspire end-to-end smoke is temporarily not a required CI check. A green build does not demonstrate runtime security or startup readiness. Restore this gate and validate generated AppHost certificate behavior and OpenViking non-root bind-mount permissions before an Aspire-enabled release; see `docs/release-readiness.md`.
