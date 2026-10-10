# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M1–M8 are complete against the current CI and distribution gates.** HermesStack provides secure isolated AI workspaces with four pinned agents, memory, managed updates, recovery, observability, and optional Aspire orchestration.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara

hstack up mascara
hstack status mascara --json
hstack agent status --project mascara
hstack port add mascara 5000

hstack update check
hstack update plan
hstack backup mascara
hstack export environment.hstack --include-memory

hstack doctor mascara
```

Running `hstack` without arguments renders the dashboard and, on an interactive terminal, a Spectre.Console action menu.

## Managed updates

`hstack update check` and `hstack update plan` use the HermesStack-managed HTTPS toolchain channel rather than arbitrary upstream `latest` versions. `hstack update apply --yes` builds a candidate workspace image, recreates only previously running workspaces, validates health and agent availability, and commits the active lock only after validation. Failure triggers rollback of the active lock and a best-effort restoration of the previous running plans.

Tool versions remain image-owned. HermesStack does not officially upgrade packages in place inside running workspaces.

## Backup and portability

`hstack backup` preserves HermesStack configuration, certificates and durable operational state without copying project source directories. Full backup includes OpenViking state; `--config-only` and project-selective backup are available.

Portable export is minimal by default. `--include-memory` explicitly adds OpenViking state. Secret values are included only with `--include-secrets --passphrase-env <ENV>`; they are placed in an authenticated AES-256-GCM package using a PBKDF2-derived key and are re-protected by the destination native secret store on import.

## Project-scoped state and security

Authentication, agent state, Herdr session state, token optimizer configuration and context state remain isolated per project under `~/.hstack`. HermesStack never implicitly mounts host SSH, Docker or cloud credentials into a workspace.

Project ports bind to `127.0.0.1` by default. Corporate CA trust remains additive; TLS verification is not disabled. Secrets remain outside YAML/Compose values and are injected only into explicitly authorized agent exec processes.

Application events are written under `.hstack/logs/hstack.log` with secret redaction. Workspace logs are available through `hstack logs <project>`.

**New to HermesStack?** Start with the [French user wiki / manual](docs/INDEX.md) and the [hands-on developer tutorial](docs/guides/DEVELOPPER-AVEC-HSTACK.md) — register a source directory, run a coding agent, inspect changes, and troubleshoot common errors. The [roadmap](docs/agile/ROADMAP.md) tracks delivered and upcoming milestones.

**Next product priority (M10)**: task-oriented documentation and a **secure localhost Web UI** using .NET 10 Blazor WebAssembly + MudBlazor, with MudExtensions optional. This UI is **planned, not yet shipped**. See [EPIC-024](docs/agile/epics/EPIC-024.md) and [UI architecture/roadmap](docs/product/WEB-UI-PLAN.md).

## Development workflow

Every pull request runs Linux and Windows build/unit validation. Linux also runs integration tests and required M3–M8 regression/runtime gates. A successful push to `main` publishes self-contained `win-x64`, `linux-x64` and `linux-arm64` delivery artifacts with SHA-256 manifests.

## M8 Aspire Experience — complete (CI validation)

M8 was integrated through PR #12 and closed by PR #15. The pinned Aspire CLI 13.6.0 starts the real secure non-root/read-only workspace with the full agent image and OpenViking, while Docker Compose remains supported. All required M3–M8 Linux gates and Windows compilation/unit tests passed on the **merged main commit** `cf6397a6543c7e74bb231ca085136425399a9d6e`; [CI #139](https://github.com/jcambert/HStack/actions/runs/38048112197) also published the `win-x64`, `linux-x64`, and `linux-arm64` packages with SHA-256 verification.

This certifies the supported GitHub Actions CI/release workflow; it is not a claim of exhaustive testing on every target device. Existing OpenViking installs with root-owned bind-mounted state require a backed-up, controlled ownership migration before non-root startup. See the [M8 report](docs/agile/M8-REPORT.md) and [release readiness](docs/release-readiness.md).
