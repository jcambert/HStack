# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M7 Operations is complete on the current release branch.** HermesStack keeps the secure isolated M1-M6 runtime and adds managed updates, recovery/portability, operational observability, automation-friendly CLI flows and multi-platform delivery.

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

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M7-REPORT.md`.

## Development workflow

Every pull request runs Linux and Windows build/unit validation. Linux also runs integration tests and M3-M7 regression gates. A successful push to `main` publishes self-contained `win-x64`, `linux-x64` and `linux-arm64` delivery artifacts with SHA-256 manifests.

## M8 Aspire validation and release gate (temporary CI exception)

The PR/main CI deliberately runs the .NET solution build, unit tests on Linux and Windows,
Linux integration tests, and M3–M7 regression gates. The **M8 Aspire end-to-end smoke**
(`tests/e2e/m8-smoke.sh`) is **temporarily excluded from the required CI path**:
its generated Aspire AppHost currently fails during Linux startup with the experimental
certificate diagnostic `ASPIRECERTIFICATES001` / read-only-container certificate injection.
This exception allows M8 application code to be integrated but **does not certify Aspire
runtime readiness**. The Windows build/unit gate remains required and must not be skipped.

**Release blocker / follow-up:** Before claiming M8/Aspire production readiness or shipping
an Aspire-enabled release, fix the generated AppHost project in
`AspireDeploymentPlanWriter.cs` so its certificate configuration compiles and runs,
verify the workspace starts under the read-only root filesystem without relaxing
container security, and re-enable the M8 smoke as a required CI gate. Re-test OpenViking
startup with non-root secret/config bind-mount permissions and validate the full agent
image build (the CI smoke currently substitutes the base image to avoid upstream GitHub
HTTP 429 throttling). Record successful Linux M8, Windows, and release-artifact checks
in the release notes before shipping. Until then, Aspire is **not release-validated**.
