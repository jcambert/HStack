# M8 Aspire Experience — validation report

**Status (2026-10-10): runtime-validated on PR #15; release-artifact and post-merge validation pending.**

## Integration and runtime evidence

- PR #12 integrated EPIC-021 on main (2026-10-09).
- PR #15 fixes the generated AppHost project: the single experimental `ASPIRECERTIFICATES001` diagnostic is allowed without disabling `TreatWarningsAsErrors`.
- [CI #136](https://github.com/jcambert/HStack/actions/runs/38047248670) succeeded (Linux + Windows) for the PR candidate:
  - .NET builds, unit and integration tests, and M3–M7 regression gates;
  - pinned Aspire CLI 13.6.0 and the **required** `tests/e2e/m8-smoke.sh` gate;
  - actual Aspire AppHost compilation, workspace startup, dashboard/status, and orchestrator switching;
  - Docker inspection: non-root workspace, read-only root, all capabilities dropped, no privileges, no Docker socket, and loopback-only publishing;
  - OpenViking startup under the host owner's numeric non-root Linux identity, 0600 private config/secret files, and health diagnostics;
  - the *real full agent image*, built and exercised by M3 on the same Linux runner, re-used by M8 only after label/version/hash verification. No base-image substitution.
- Windows build and unit tests also succeeded in CI #136.

## Remaining release checks

- [ ] PR candidate packaging for `win-x64`, `linux-x64`, and `linux-arm64` passes with verified SHA-256 manifests (now required by `publish-artifacts` on PRs).
- [ ] Merge the **tested head SHA** only after Linux M8, Windows, and artifacts are all green.
- [ ] Re-check the post-merge `main` workflow and its three delivery artifacts.
- [ ] Update the final release note / roadmap to the observed post-merge result.

Security remains mandatory across Compose and Aspire. Neither `WithoutHttpsCertificate()`'s scoped compile-time diagnostic nor its avoidance of Aspire certificate injection disables HStack corporate CA trust or TLS verification.

## Operating constraints

The Linux CI validates non-root OpenViking on a fresh, runner-owned data root. Existing installations with root-owned historical OpenViking data may need a **controlled ownership migration** after backup; do not widen file permissions to 0777 or disclose the root key.

M9 remains deferred until the M8 release checks are closed.
