# Release readiness and CI policy

## Current state — 2026-10-10

M1–M7 are complete. M8's implementation is integrated, and its **Aspire runtime gate is passing on PR #15**: see [Linux/Windows CI #136](https://github.com/jcambert/HStack/actions/runs/38047248670). An Aspire-enabled release still requires successful multi-RID artifacts and post-merge main validation; do not claim release readiness from a PR alone.

## Mandatory acceptance checklist

- [x] Generated AppHost compiles with a *targeted* `ASPIRECERTIFICATES001` exception, all other warnings as errors.
- [x] Aspire launches the real workspace with non-root execution, read-only root, dropped capabilities, no privileged mode and loopback-only ports (CI #136).
- [x] HStack CA/TLS policy remains intact; no certificate-validation bypass was added.
- [x] OpenViking starts with a non-root identity, private 0600 config and root-key files, and health checks (CI #136).
- [x] Full pinned agent image built in M3 and verified/reused in M8, without substitution by the base image (CI #136).
- [x] `tests/e2e/m8-smoke.sh` is required by Linux CI and passed (CI #136).
- [x] Windows build and unit tests passed on the PR candidate (CI #136).
- [ ] `win-x64`, `linux-x64`, `linux-arm64` PR release-candidate archives and SHA-256 verification passed.
- [ ] Post-merge `main` workflow, all three release archives, and SHA-256 manifests passed.
- [ ] Release notes include the observed CI SHA and known limitations.

## Rules

Normal pull requests run Linux restore/build/unit/integration, M3–M8 regression/runtime gates, Windows restore/build/unit, and cross-platform distribution candidate packaging. A failed mandatory gate blocks merge; security requirements must never be relaxed for parity.

On Linux, OpenViking adopts the numeric non-root host owner of its private bind mounts. Migrating previously root-owned OpenViking state requires a backup and controlled ownership correction, not permissive chmod.
