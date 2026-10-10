# EPIC-023 — External Sandbox Providers (M9)

**Status:** Implementation complete in PR #19 candidate, green PR CI #146; awaiting post-merge main CI before formal closure. **Predecessor:** M8 completed. **Successor:** M10 UI (cannot start until M9 is Done).

## Intent and explicit boundary

Introduce a narrow, composable execution-environment provider layer without replacing **Compose/Aspire orchestration** or weakening HStack's existing native Docker isolation. The V5 source names **PBI-2301..PBI-2305**; Dagger Container Use and DevPod are **evaluation spikes**, not mandatory product integrations.

| Work item | Goal | Dependency |
| --- | --- | --- |
| [PBI-2301](../pbi/PBI-2301.md) | Execution provider abstraction and capability negotiation | M8 |
| [PBI-2302](../pbi/PBI-2302.md) | Native Docker container provider / compatibility | 2301 |
| [PBI-2305](../pbi/PBI-2305.md) | Provider security parity and real CI tests | 2302 |
| [PBI-2303](../pbi/PBI-2303.md) | Dagger Container Use feasibility spike | Stable 2302, initial 2305 evidence |
| [PBI-2304](../pbi/PBI-2304.md) | DevPod feasibility spike | Stable 2302, initial 2305 evidence |

Execution order is **2301 → 2302 → 2305 → 2303/2304 → M9 review**. PBI identifiers remain unchanged even when dependency order differs.

## Architecture direction (source: V5 §223-225)

```text
WorkspaceDeploymentPlan
 ├── IWorkspaceOrchestrator: Compose | Aspire     (resource lifecycle/topology)
 └── IExecutionEnvironmentProvider: NativeDocker (workspace isolation)
                                       └── optional providers evaluated in spikes
```

Proposed `IExecutionEnvironmentProvider` operations: `Id`, `DetectAsync`, `PlanAsync`, `CreateAsync`, `DestroyAsync`. Adapt existing service abstractions rather than adding a second parallel lifecycle owner. Maintain a central capability compatibility result across orchestrator, execution provider and agent integration. Mandatory security capabilities cannot degrade: `Unsupported` / `Forbidden` must **block** startup; no silent fallback.

## Required security invariants

- No Docker socket or host Docker pipe inside agent workspaces; no privileged containers or host namespaces.
- Non-root, read-only root filesystem, `no-new-privileges`, `cap-drop=ALL`.
- Precise project read/write mount, dedicated per-project agent/session/context state; no implicit host HOME or secrets.
- Loopback-only published ports, no TLS verification bypass, CA trust additive.
- Deterministic detection/availability and clear diagnostics; no surprise provider migration.
- Regression coverage of Compose, Aspire, agents, Herdr, OpenViking and project state persistence.

## Research criteria

Compare Dagger Container Use and DevPod with the **native baseline** on Windows/Docker Desktop and Linux, integration fit, proxy/CA, filesystem/secret/identity boundaries, lifecycle, portability, performance, operational maintenance and upstream maturity. Record reproducible spike steps, explicit missing capabilities and a **Go / No-Go / revisit later** conclusion. **No full integration is required** to close M9 if evidence supports No-Go.

## M9 Definition of Done / gate for M10

- PBI-2301/2302 delivered with tested compatibility and no new security exemptions.
- PBI-2305 tests demonstrate required invariants in actual target runtime, fail-closed policies and no M1–M8 regression.
- PBI-2303/2304 spike reports with reproducible criteria and decision, after native stable.
- Linux runtime CI + Windows build/test, appropriate package verification and updated user/developer docs all green on the exact evaluated commit.
- Publish `docs/agile/M9-REPORT.md` with evidence, constraints, platform limits, decisions and next actions. Only then mark M9 Done and authorize implementation of M10.
- Use consolidated coherent PRs/commits and avoid unnecessary GitHub CI reruns: **one validated change set per logical milestone increment**, not a push per documentation line.

**Gate evidence:** PR #19 head `fce766dc0015642eab0f31b24322c5f6b21c4b5d` passed [CI #146](https://github.com/jcambert/HStack/actions/runs/38075662535) (Linux Compose/Aspire E2E, Windows tests, 3 checksum-verified packages). A green post-merge main workflow is still mandatory to close M9. Dagger and DevPod findings are desk-based No-Go adoption decisions, not executed external PoCs.
