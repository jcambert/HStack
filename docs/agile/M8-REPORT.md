# M8 Aspire Experience — Integration report

**Status (2026-10-09): integrated into main, release validation pending.**

## Integration evidence

- PR #12 merged to main with squash commit `e43beefc5cf1c3b6deed8d348134ef813ab7310c`.
- CI run #125 passed Linux and Windows build/unit checks, Linux integration tests and M3–M7 regression smoke checks.
- The M8 Aspire end-to-end smoke was deliberately removed from required CI for this integration; this is **not** evidence of Aspire runtime success.

## Open issues / release blockers

1. Fix generated Aspire AppHost configuration in `AspireDeploymentPlanWriter.cs` to explicitly handle the experimental `WithoutHttpsCertificate()` API (`ASPIRECERTIFICATES001`).
2. Demonstrate workspace startup under the mandatory read-only filesystem without weakening container isolation or TLS trust.
3. Revalidate OpenViking startup and non-root access to mounted secret/config files; the CI smoke currently skips its startup.
4. Exercise the complete agent-image build instead of the CI base-image substitution that avoids upstream HTTP 429 throttling.
5. Re-enable `tests/e2e/m8-smoke.sh` as a required CI gate and capture passing Linux M8 and Windows checks.
6. Verify release artifacts and SHA-256 manifests for win-x64, linux-x64 and linux-arm64 before Aspire-enabled delivery.

## Follow-up: generated AppHost certificate diagnostic (2026-10-10)

Inspection of the source revealed a mismatch: the checked-in
`src/HermesStack.AppHost/HermesStack.AppHost.csproj` suppresses the single
experimental `ASPIRECERTIFICATES001` diagnostic, but the runtime project
emitted by `AspireDeploymentPlanWriter` did not. It also treats warnings
as errors, preventing generated AppHost compilation.

A focused fix is proposed on a separate branch to add
`<NoWarn>$(NoWarn);ASPIRECERTIFICATES001</NoWarn>` to the generated
project, preserving `TreatWarningsAsErrors`. Unit regression coverage checks
the generated project and the fail-closed read-only-root policy.

**Evidence still required:** CI .NET build/unit results; generated AppHost
compilation with the pinned Aspire SDK; real read-only workspace startup;
OpenViking non-root mount validation; full agent image; M8 required CI smoke.
This follow-up does **not** make the Aspire-enabled release ready.

## Decision

Merge of M8 implementation is complete. **Aspire-enabled release remains blocked** until the above acceptance criteria are met. See `docs/release-readiness.md` for the delivery checklist.
