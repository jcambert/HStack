# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M4 Network & Security is complete.** HermesStack now combines the existing isolated agent/session runtime with validated corporate proxy/CA support, protected project secrets, live security inspection and actionable doctor diagnostics.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara

hstack proxy set --https http://proxy.corp:8080 --no-proxy corp.internal
set OPENAI_API_KEY=...
hstack secret set OPENAI_API_KEY --project mascara --agents codex --from-env OPENAI_API_KEY

hstack up mascara
hstack security inspect mascara
hstack doctor mascara
```

Secrets are never written into `hstack.yaml`, `projects.yaml` or Compose. Policy metadata records only the project, secret name and allowed agents. Direct agent launches receive authorized values only for the lifetime of that exec process.

Herdr 0.9.3 continues to own persistent project sessions for Claude Code, Codex, Hermes Agent and OpenCode, with tmux as a fallback.

## Project-scoped state

Authentication, agent state and Herdr session state remain isolated per project under `~/.hstack/data/projects/<project>/`. HermesStack never implicitly mounts the host user's agent, SSH, Docker or cloud credentials into the workspace.

Corporate CA trust is additive; HermesStack does not disable TLS verification. Proxy endpoints containing inline credentials are rejected.

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M4-REPORT.md`.

## Development workflow

Every pull request runs Linux and Windows validation. Linux also executes the M3 regression gate and the current M4 Docker end-to-end gate. A successful push to `main` repeats validation and then publishes `win-x64`, `linux-x64` and `linux-arm64` artifacts with SHA-256 manifests.
