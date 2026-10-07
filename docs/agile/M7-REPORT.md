# M7 Operations — Completion Report

## Scope
M7 covers EPIC-016 through EPIC-020: managed updates, backup/restore/portability, operational observability, CLI operational UX and CI/distribution.

## Delivered
- Managed HTTPS update inventory and deterministic plan.
- Transactional workspace/toolchain apply flow with config backup, candidate image validation, running-workspace recreation, agent health checks and rollback.
- Active toolchain lock override for reproducible managed updates.
- Backup/restore, selective project backup, config-only backup and portable export/import with path remapping.
- Portable memory is opt-in with `--include-memory`.
- Optional secret portability uses AES-256-GCM with a PBKDF2-derived key and rehydrates values through the destination native secret store.
- Full backups include durable OpenViking state; project source trees are excluded.
- Application log under `.hstack/logs/hstack.log` with secret redaction.
- Workspace logs through `hstack logs`.
- Interactive Spectre.Console dashboard, project list/show/add/edit/remove, loopback port management, explicit agent status, restart, config validation, JSON status, safe Compose passthrough and managed cleanup.
- Linux/Windows validation and self-contained win-x64/linux-x64/linux-arm64 delivery artifacts with SHA-256 manifests.

## Safety invariants
- No update uses upstream `latest`.
- Update metadata remains HTTPS-only and parsed by the pinned toolchain validator.
- Candidate workspace image is health-checked before replacing the active tag.
- Rollback restores the prior active lock and attempts to recreate previously running workspaces with their old plan.
- Portable export excludes secrets by default; `--include-secrets` requires a passphrase environment source and an authenticated encrypted package.
- Restore rejects unsupported/traversal archive entries.
- Cleanup targets only stopped containers with `io.hstack.managed=true`.
- Project removal unregisters metadata and never deletes project source.

## Validation
M7 is complete when the feature PR is green on the Linux and Windows jobs, including the M7 operations smoke gate.
