# Release readiness and CI policy

## Current state — 2026-10-10

**M8 Aspire Experience: delivered and validated by the repository's mandatory CI/release gates.**

- [PR #15](https://github.com/jcambert/HStack/pull/15) squash-merged to `main` at commit `cf6397a6543c7e74bb231ca085136425399a9d6e`.
- [PR CI #138](https://github.com/jcambert/HStack/actions/runs/38047683192): Linux + Windows + three platform release-candidate jobs passed at tested head `41a0e8999610e11847fc05e904c849ffa437416f`.
- [Post-merge main CI #139](https://github.com/jcambert/HStack/actions/runs/38048112197): **five out of five jobs passed** at the merge commit.
- GitHub Actions #139 published three verified artifacts: `hstack-win-x64`, `hstack-linux-x64`, `hstack-linux-arm64`. Their package step ran `sha256sum -c` successfully for each RID.

## Acceptance checklist

- [x] Generated AppHost compiles with targeted `ASPIRECERTIFICATES001` suppression and other warnings as errors.
- [x] Pinned Aspire starts a real workspace with read-only filesystem, non-root identity, dropped Linux capabilities, non-privileged mode, no Docker socket and loopback-only host bindings.
- [x] Corporate CA and TLS verification remain enabled; no insecure certificate bypass was added.
- [x] OpenViking startup and health pass using non-root access to 0600 config and root-key files on the Linux runner.
- [x] Complete pinned agent image built/verified in M3 and reused/checked in M8, with no base-image substitution.
- [x] `tests/e2e/m8-smoke.sh` is a required Linux CI gate and passed on `main`.
- [x] Windows build/unit passed on `main`.
- [x] Three release candidate archives and SHA-256 checks passed on the PR.
- [x] All post-merge `main` jobs and three artifact uploads passed.
- [x] Evidence and known limitations recorded here and in `docs/agile/M8-REPORT.md`.

## Scope and limitations

The success of CI #139 demonstrates the repository's validated delivery workflow, **not certification on every end-user host or physical ARM64 machine**. Linux ARM64 has cross-published archives/checksums, but M8's real runtime smoke is performed on Linux x64 in GitHub Actions. Windows compiles and runs unit tests; it does not run the Linux OCI runtime smoke.

The pinned OpenViking image defaults to root upstream; HStack now runs it using the Linux host owner's UID/GID for owner-readable private bind mounts. Existing root-owned OpenViking data may need a backed-up, controlled ownership correction; do not resort to world-writable permissions.

A green M8 release does not automatically close M9 or guarantee availability of external upstream services.

## Continuing CI policy

Every PR/push requires Linux build/unit/integration and M3–M8 gates plus Windows build/unit. The artifact matrix publishes `win-x64`, `linux-x64`, `linux-arm64` self-contained packages with `sha256sum -c` verification. Failed mandatory gates block delivery.

## M10 Local UI release gate — 2026-10-10

- [x] [PR #20](https://github.com/jcambert/HStack/pull/20) merged; candidate [CI #158](https://github.com/jcambert/HStack/actions/runs/38084113404) 5/5 success and post-merge [main CI #159](https://github.com/jcambert/HStack/actions/runs/38084607822) 5/5 on `91f6f31c9da641ecb8cc5f9dac0ec7f5722a1bb9`.
- [x] Required existing M3–M9 Linux Compose/Aspire/OpenViking runtime gates preserved, Windows compile/unit green.
- [x] HTTPS loopback API, hostile origin/host/CSRF/replay rejection and real Chrome WebAssembly render tested under Linux CI.
- [x] Packaged `web/` host and static assets verified in SHA-256-checked `win-x64`, `linux-x64`, `linux-arm64` CI artifacts. These are **Actions artifacts**, not a tagged GitHub Release.
- [ ] Native Windows browser interactive test and native ARM64 OCI/Web UI runtime validation (not claimed).
- [ ] Independent accessibility audit and end-user trial on Windows (future quality follow-up).

See [M10 report](agile/M10-REPORT.md) for scope and limitations.
