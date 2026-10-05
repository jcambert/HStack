# HermesStack architecture

HermesStack is a local secure control plane. It owns orchestration, policy, lifecycle and diagnostics; specialized upstream tools retain ownership of agent behavior, authentication, session protocols, context and token filtering.

```mermaid
flowchart TD
  Host[Host] --> H[hstack control plane]
  H --> Config[Proxy + CA policy]
  H --> Secrets[Protected secret store]
  H --> Plan[WorkspaceDeploymentPlan]
  H --> Registry[IAgentHarnessRegistry]
  H --> Sessions[HerdrSessionService]
  H --> Inspect[Security inspection + doctor]
  Plan --> Orch[IWorkspaceOrchestrator]
  Registry --> Orch
  Sessions --> Orch
  Orch --> Compose[Docker Compose]
  Compose --> Workspace[Project workspace container]
  Secrets -. authorized exec only .-> Registry
  Workspace --> Herdr[Herdr named project session]
  Herdr --> Claude[Claude Code pane]
  Herdr --> Codex[Codex pane]
  Herdr --> Hermes[Hermes Agent pane]
  Herdr --> OpenCode[OpenCode pane]
  Workspace --> Tmux[tmux fallback]
  Workspace -. M5 .-> RTK[RTK]
  Workspace -. M6 .-> OV[OpenViking]
```

## Deployment source of truth

Configuration is validated before a `WorkspaceDeploymentPlan` is built. Compose only renders and executes that validated plan. M4 adds normalized proxy variables to the plan but deliberately keeps secret values out of it.

## Agent boundary

Each direct agent integration remains an `IAgentHarness`. Direct `hstack claude|codex|hermes|opencode` remains the fallback path alongside Herdr-managed sessions.

M4's `SecretInjectionService` resolves values only for the selected project and agent. The Docker orchestrator passes only secret names as `docker compose exec -e NAME` arguments while the actual values live in the Docker CLI process environment. This avoids both Compose persistence and command-line value exposure.

## Session boundary

Each project receives the Herdr session name `hstack-<project-id>` and logical workspace label `hstack:<project-id>`. Herdr state lives under that project's mounted HOME. tmux remains a fallback and HermesStack does not nest Herdr inside tmux.

## Inspection boundary

`WorkspaceDeploymentPlan` remains the declared policy authority. `DockerWorkspaceSecurityInspector` reads effective container state when available. `SecurityInspectionService` applies deterministic scoring, and `doctor` combines host, network, certificate, agent/session and security diagnostics.
