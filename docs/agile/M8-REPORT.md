# M8 Aspire Experience — completion report

**Status (2026-10-10): DONE — PR #15 merged and complete post-merge release workflow validated.**

## Traceability

- Original Aspire integration: PR #12.
- M8 release completion: [PR #15](https://github.com/jcambert/HStack/pull/15), squash merge `cf6397a6543c7e74bb231ca085136425399a9d6e`.
- [PR CI #138](https://github.com/jcambert/HStack/actions/runs/38047683192): **5/5** jobs passed on tested head `41a0e8999610e11847fc05e904c849ffa437416f`.
- [Main CI #139](https://github.com/jcambert/HStack/actions/runs/38048112197): **5/5** jobs passed on merged commit `cf6397a6543c7e74bb231ca085136425399a9d6e`.

## Implemented and validated

- Scoped fix to the dynamically generated AppHost project for the experimental `ASPIRECERTIFICATES001` diagnostic; normal warnings remain errors.
- Linux `m8-smoke.sh` reinstated as a **required** CI gate with Aspire CLI 13.6.0.
- Real Aspire AppHost build/start/status/dashboard and orchestration switching passed.
- Actual workspace OCI checks passed: non-root identity, read-only root filesystem, no privileged mode, dropped capabilities, no Docker socket and loopback-only host ports.
- OpenViking started and reported healthy with a non-root UID/GID and owner-private 0600 configuration/root-key source.
- The genuine pinned full agent image was built in M3 and reused/checked in M8 via immutable toolchain digest/image labels, rather than substituting the base image.
- Windows restore/build/unit, Linux build/unit/integration and M3–M8 gates passed on `main`.
- Release artifacts `hstack-win-x64`, `hstack-linux-x64` and `hstack-linux-arm64` were packaged, checksum-verified with `sha256sum -c` and uploaded successfully in CI #139.

## Remaining operational limitations (not blockers for CI milestone closure)

- Existing root-owned OpenViking files require backed-up, controlled ownership migration when adopting the non-root container; never loosen credential access permissions.
- Runtime smoke was exercised on the GitHub Linux x64 runner, **not** natively on Linux ARM64 or Windows hosts. The latter platforms are covered by their declared package/build/unit gates only.
- Aspire certificate auto-injection is intentionally suppressed because of the read-only root filesystem; HStack-owned corporate CA/TLS verification is preserved.
- Upstream tool downloads may be rate-limited. M8 CI reuses the real full agent image already built/tested by M3 rather than silently substituting a base image.

## Decision

**Close M8 for the implemented CI/release acceptance criteria.** Keep platform-specific runtime matrix expansion and legacy OpenViking ownership migration as future operational hardening; M9 remains deferred pending prioritization.
