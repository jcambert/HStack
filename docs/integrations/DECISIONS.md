# Build vs Integrate decisions

| Capability | Existing tool | Strategy | HermesStack responsibility | Security / maintenance rationale | Fallback |
|---|---|---|---|---|---|
| Workspace policy | none suitable | BUILD | Own policy + validation | Core product boundary | n/a |
| Compose lifecycle | Docker Compose | INTEGRATE | Generate validated plan, invoke CLI | Avoid reimplementing engine | fail with diagnostics |
| Aspire lifecycle | Aspire | INTEGRATE (later) | Adapter only | Use first-party resource model/dashboard | Compose |
| Agent sessions | Herdr | INTEGRATE | Install/configure/launch/persist | Do not build a second multiplexer | direct agent launch |
| Long-term context | OpenViking | INTEGRATE | Deploy, scope, policy, backup | Do not build vector DB/memory engine | memory disabled |
| Shell output reduction | RTK | INTEGRATE | Select/configure/measure | Upstream owns rewrite/filter logic | optimizer off |
| Agent CLIs | upstream agents | INTEGRATE | Pin/install/configure/launch | Upstream auth/update semantics | disable unavailable agent |
| Corporate CA/proxy | platform primitives | BUILD | Own policy + propagation | Security-critical local responsibility | explicit diagnostics |
| Secrets | OS-protected stores | WRAP | Abstract policy + per-agent injection | Never store plaintext config | no secret injection |
| Native sandbox | Docker container | BUILD/WRAP | M1 execution provider | Lowest dependency path | future VM/remote provider |
| Dagger Container Use | Dagger | DEFER/SPIKE | Evaluate provider | Avoid premature dependency | native container |
| DevPod | DevPod | DEFER/SPIKE | Evaluate provider | DevContainer path is optional | native container |
