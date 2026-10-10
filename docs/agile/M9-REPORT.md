# M9 — Optional Sandbox Providers validation report

**Milestone:** M9 / EPIC-023. **Candidate state:** code and research prepared; final acceptance depends on green Linux/Windows CI for the exact code commit and confirmed merge to main. Do not read this pre-merge document as evidence that CI already passed.

## Implementation

- PBI-2301 — `IExecutionEnvironmentProvider` with explicit `DetectAsync`, `PlanAsync`, `CreateAsync` and `DestroyAsync`, plus typed plan/handle. Compose/Aspire remain distinct orchestrators.
- PBI-2302 — `NativeContainerExecutionProvider` registered as the **sole supported runtime**, preserving the existing orchestrator and Docker resource owners. The routed path checks the plan before up/restart/preview; teardown remains possible even when a previously created plan is no longer acceptable. No settings migration, project format change, image-version change or credential movement.
- PBI-2305 — security fail-closed admission: deny privileged, disabled no-new-privileges, non-dropped caps, writable root, host network/PID/IPC, external bind ports, Docker socket/pipe (source and target), host filesystem root mounts, non-isolated project agent state, unknown orchestrators. Existing Compose/Aspire writers continue to enforce their own policy as defense in depth.
- PBI-2303 / 2304 — researched but **not integrated**. Explicit **NO-GO for V5 adoption**, with evidence/gaps, next PoC instructions and honest limits in [Dagger research](../research/m9-dagger-container-use-2026-10-10.md) and [DevPod research](../research/m9-devpod-2026-10-10.md). These are desk studies, **not** claims of an executed upstream PoC.

## Validation and regression matrix

| Coverage | Evidence / requirement |
| --- | --- |
| Unit | `NativeContainerExecutionProviderTests`: Compose + Aspire up/restart/down/preview/status passthrough, no added runtime probing, wrong-provider rejection, fail-closed matrix, cleanup of invalid plans, Docker explicit probe |
| Compose real OCI | Required `tests/e2e/m3-smoke.sh` verifies agent toolchain, project private mounts, working Git sessions, non-root, read-only, cap-drop, no-privileges and durable Herdr restore |
| Aspire + OpenViking real OCI | Required `tests/e2e/m8-smoke.sh` verifies pinned Aspire 13.6, non-root container, OpenViking private ownership, Docker and host namespace security, all four agents, memory, tokens, sessions and orchestration switching |
| Linux/Windows | `.github/workflows/ci.yml` existing required Linux integration/e2e and Windows compile/unit gates; no gates removed |
| Packaging | CI publishes SHA256-verified win-x64, linux-x64, linux-arm64 artifacts; native ARM64 runtime remains untested in CI |
| External providers | **None shipped**; no Dagger/DevPod binaries, runtime access, credentials, mounts or services enabled |

## What was intentionally not changed

- Compose/Aspire scripts, OCI options, project YAML, user commands, image versions, OpenViking auth or storage model.
- Herdr/sessions, agents/Codex auth, RTK, corporate CA handling and TLS settings.
- No silent fallback, auto-upgrade or new provider selection by users.
- Existing Linux M3–M8 runtime gates stay required and run on the same agent image (no duplicate full-image rebuild for M9).

## Remaining caveats / acceptance

- The existing Linux x64 Docker/Aspire runner is the live OCI security baseline; Windows remains build/test, ARM64 remains cross-publish/package SHA validation. Native Windows/Aspire or ARM64 runtime equivalence cannot be asserted.
- No external Container Use or DevPod PoC was executed. Their adoption remains blocked; a dedicated future evaluation may be funded if product needs justify it.
- Mark M9 **Done** and allow M10 only after the GitHub Actions PR candidate and post-merge main gates are verified green and the exact SHA/evidence is recorded in a closure update.
