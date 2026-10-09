# Release readiness and CI policy

## Current state — 2026-10-09

PR #12 was merged on main (`e43beefc5cf1c3b6deed8d348134ef813ab7310c`). CI #125 passed its configured Linux and Windows jobs. The M8 Aspire runtime smoke is **temporarily excluded**, so this green CI validates integration but **does not authorize an Aspire-enabled release**.

## Required checks for normal pull-request integration

- .NET solution restore/build on Linux and Windows; unit tests on both platforms.
- Linux integration tests and M3–M7 regression smoke tests.
- Do not silently remove Windows coverage or security-related gates.

## Aspire-enabled release acceptance checklist

- [ ] Generated AppHost handles `WithoutHttpsCertificate()` / `ASPIRECERTIFICATES001` and compiles.
- [ ] Aspire starts the workspace with read-only root, non-root user, dropped capabilities, no privileged mode and loopback-only ports.
- [ ] CA trust and TLS verification remain intact; no insecure certificate bypass.
- [ ] OpenViking starts with correct non-root secret/config mount permissions.
- [ ] Full agent image build succeeds without CI base-image substitution.
- [ ] M8 end-to-end smoke re-enabled as a **required** Linux CI gate and passes.
- [ ] Windows build and unit tests pass on the release candidate.
- [ ] win-x64, linux-x64 and linux-arm64 release artifacts build and SHA-256 manifests are verified.
- [ ] Release notes identify validated features, test evidence and remaining limitations.

Do not label M8 as production-ready, or ship an Aspire-enabled release, before these checks are completed. Track progress in `docs/agile/ROADMAP.md` and `docs/agile/M8-REPORT.md`.
