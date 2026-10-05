# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M5 Token Efficiency is complete.** HermesStack now layers token optimizers onto the isolated M1-M4 runtime without taking ownership of their filtering algorithms.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara

hstack token providers
hstack token enable mascara --provider rtk --profile balanced --agents claude,codex
hstack token status mascara
hstack token gain mascara
hstack token doctor mascara
```

RTK v0.51.0 is the default optimizer. Caveman v2.7.0 is available only as an explicit aggressive/custom option. RTK + Caveman is classified PotentiallyLossy and requires `--allow-lossy-stack`.

## Token metric semantics

RTK gain represents shell-output reduction converted to estimated context tokens. HermesStack labels it **Estimated** and does not call it LLM bill savings.

RTK's upstream command-history database is redirected to workspace `/tmp`, raw-output recall is disabled, and telemetry is disabled. HermesStack persists only sanitized aggregate gain records.

## Project-scoped state

Authentication, agent state, Herdr session state and token optimizer configuration remain isolated per project under `~/.hstack`. HermesStack never implicitly mounts host SSH, Docker or cloud credentials into a workspace.

Corporate CA trust remains additive; TLS verification is not disabled. Secrets remain outside YAML/Compose values and are injected only into explicitly authorized agent exec processes.

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M5-REPORT.md`.

## Development workflow

Every pull request runs Windows validation and the Linux M3/M4/M5 gates. A successful push to `main` repeats validation and then publishes `win-x64`, `linux-x64` and `linux-arm64` artifacts with SHA-256 manifests.
