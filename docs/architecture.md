# HermesStack architecture

HermesStack is a local secure control plane. It owns orchestration, policy, lifecycle and diagnostics; specialized upstream tools retain ownership of sessions, agent behavior, context and token filtering.

```mermaid
flowchart TD
  Host[Host] --> H[hstack control plane]
  H --> Plan[WorkspaceDeploymentPlan]
  Plan --> Orch[IWorkspaceOrchestrator]
  Orch --> Compose[Docker Compose]
  Orch -. future .-> Aspire[Aspire]
  Compose --> Workspace[Project workspace container]
  Workspace --> Herdr[Herdr]
  Herdr --> Hermes[Hermes]
  Herdr --> Claude[Claude Code]
  Herdr --> Codex[Codex]
  Herdr --> OpenCode[OpenCode]
  Workspace -. future .-> RTK[RTK]
  Workspace -. scoped API .-> OV[OpenViking]
```

## M1 source of truth

Configuration is validated before a `WorkspaceDeploymentPlan` is built. The Compose backend only renders and executes the validated plan. Security rules therefore do not belong to Compose-specific code.
