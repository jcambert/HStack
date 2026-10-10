# M9 research spike — Dagger Container Use (PBI-2303)

**Decision for V5:** **NO-GO for adoption as an HStack execution provider now**. Revisit only after native provider/security parity is stable and a separate, explicitly isolated integration PoC passes. This is a **documented desk research / risk spike**, not an executed Container Use integration test.

**Baseline:** HStack native Docker with Compose/Aspire, four pinned agents, private project-scoped state, no privileged/docker socket, 127.0.0.1 port policy, CA/TLS, Herdr and OpenViking, as exercised by M3–M8 CI.

## Primary upstream evidence (checked 2026-10-10)

- https://github.com/dagger/container-use — early-development MCP/CLI with isolated container + Git branch environments, logs, independent agents and optional terminal intervention.
- https://github.com/dagger/container-use/blob/main/docs/environment-configuration.mdx — configurable base image, environment and setup commands; baseline is Ubuntu and can be customized, but adopting the HStack **pinned** agent image needs its own validation.
- https://github.com/dagger/container-use/blob/main/AGENTS.md — Git branch and notes/worktree state ownership, which differs from HStack's current direct-host-project bind-mount model.
- https://docs.dagger.io/reference/api/container/ — Dagger API includes powerful container execution options; no security parity may be assumed from Dagger being container-based.

## Findings / gap versus mandatory HStack behavior

| Topic | Upstream documented | HStack conclusion for V5 |
| --- | --- | --- |
| Separate per-agent environments | Yes (containers and branches) | Potential benefit; different Git working-copy semantics |
| Git/agent history | Branches + notes/worktrees; explicit checkout flow | Must not silently replace direct changes in /workspace |
| MCP interface | Yes; `container-use stdio` | Fit for MCP agents; Herdr/Codex/Hermes/OpenCode integration not proven |
| Custom image / toolchain pins | Base image configurable | Exact signed/digest-pinned image parity unverified |
| Host/proxy/corporate CA | Requires deployment-specific testing | Unverified |
| Read-only root, all caps dropped, non-root, no daemon socket/host namespaces | No HStack-specific proof | **Block adoption** until live OCI checks pass |
| OpenViking, RTK, Herdr, project state | Not part of the upstream HStack integration | Unverified |
| Windows + Docker Desktop | Explicit test required | Unverified here |
| Performance/maintenance | Extra Dagger/MCP management | Not measured |

## Reproducible follow-up PoC (not run in this work)

On a throwaway **non-sensitive repository** with Docker and Git, install a **reviewed and pinned** Container Use release using upstream documentation; do not blindly pipe an installer into a trusted host shell. Configure an MCP client for `container-use stdio`, request a non-sensitive edit, inspect with `container-use log <environment-id>` and `container-use checkout <environment-id>`, then inspect every created container with `docker inspect` and cross-check the HStack baseline.

Required proof: exact image and toolchain pins; no Docker socket or host mount except explicitly allowed project storage; non-root and read-only root; dropped capabilities; no host namespaces; loopback-only ports; no host agent credentials; no TLS bypass; memory+Herdr+RTK behavior; timing measured on matched workload; tested on Windows/Docker Desktop and Linux. Fail closed if unavailable.

**PoC execution status:** *not executed*. The current tool environment cannot run third-party Docker workloads. No fabricated timings or pass claims. No external package added to HStack.

**Reasoned decision:** because M9 has no proven security-equivalent Container Use implementation, shipping this as a selectable provider would violate the core no-regression policy. No-Go is a decision about **integration now**, not a claim Container Use itself is unsafe.
