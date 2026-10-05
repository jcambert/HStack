# M1 Increment Report — Secure Foundation

## Stories completed

US-001 through US-008 required for the first secure workspace slice: initialization, project add/list, workspace up/down/shell/status, integration descriptors and Corporate CA import.

## Acceptance criteria

- one configured project maps to exactly one `/workspace` bind mount;
- a second project is not mounted or reachable through its known host path;
- Docker daemon sockets/pipes are forbidden;
- workspace runs as non-root;
- root filesystem is read-only;
- all Linux capabilities are dropped;
- `no-new-privileges` is enabled;
- default published-port policy is localhost-only;
- project HOME and certificate material are dedicated to HermesStack;
- Compose receives a validated `WorkspaceDeploymentPlan` rather than owning security policy.

## Tests executed by the promotion gate

The consolidated M1 pull request runs restore, Release build, unit tests, integration tests and `tests/e2e/m1-smoke.sh` on Ubuntu. The smoke test builds the real workspace image, initializes an isolated HermesStack data root, adds a generated Corporate CA, starts ProjectA, validates Docker runtime security, proves ProjectB is not mounted, validates the CA bundle and stops/removes the workspace.

The commit containing this report is merged to `main` only after that gate is green.

## Known limitations

- M1 contains no coding agents yet; that is M2.
- Full interactive `hstack init` wizard UX will continue to evolve; the current command is idempotent and automation-friendly.
- Full user-facing `hstack security inspect` and doctor diagnostics are M4 work.
- Docker reduces blast radius but is not a VM-grade isolation boundary.

## Security findings

The initial Node-derived Dockerfile conflicted with the upstream image's existing UID/GID 1000 user. M1 hardening reuses/renames that account and, on Linux, builds the workspace user with the invoking user's UID/GID so project and HOME binds remain writable without root.

## Backlog changes

M1 moves to Done. M2 Agent Runtime becomes Ready.

## Next recommended stories

Introduce `IAgentHarness` and integrate Claude Code and Codex first, then Hermes Agent and OpenCode, with pinned upstream versions and project-scoped state.
