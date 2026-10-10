# EPIC-024 — User Onboarding, Documentation & Secure Local Web UI

**Milestone:** M10 — planned; **Priority:** next product milestone after M8; **Status:** not implemented.

## Why

The current command-line control plane is capable but its daily developer workflow is hard to discover, even with `docs/INDEX.md`. Provide a usable, task-oriented guide **now**, then a local graphical companion for setup, projects, agents, diagnostics and daily work.

## User outcomes

- I understand what HStack does, what it does **not** do, and what runs on my PC.
- I can set up Docker, initialize HStack, register a real source folder, launch an agent, develop/test, and stop the workspace.
- When startup fails (especially Aspire CLI version mismatch), I have a step-by-step remedy.
- In the future, I can accomplish frequent tasks through a coherent accessible UI rather than memorize terminal commands.
- Experts and automation retain the same CLI and structured output.

## Architecture direction (to validate in M10.2)

- `HermesStack.Web.Client` — .NET 10 **Blazor WebAssembly**, using MudBlazor as primary design system.
- **MudExtensions** means CodeBeam's `CodeBeam.MudBlazor.Extensions`, to be added **only if** a specific control needs it. Evaluate API/version compatibility with MudBlazor 9 and .NET 10, support, bundle weight and accessibility before pinning versions.
- `HermesStack.Web.Host` — loopback-bound ASP.NET Core control API / static host, reusing **Application** services and existing orchestration abstractions (Compose/Aspire); do not shell out to `hstack.exe` as the primary integration.
- Shared typed request/response DTOs. Logs and status via bounded polling initially; consider server events only when useful.
- Documentation is **Markdown in the repository** as the source of truth, linked from the README/CLI and later rendered in the UI. A separate GitHub Wiki can mirror or link, but must not drift.

## Security invariants (mandatory, not optional UX polish)

- Browser/WASM is **untrusted**: never embed secrets, API keys, Docker endpoints, CA private material, token stores or OS execution authority in downloaded WASM.
- Serve UI + API from a **same-origin localhost** origin by default; disable LAN/remote listening until separately designed, authenticated and threat-modeled.
- API enforces authorization for every operation, anti-CSRF/origin checks and restrictive CORS; localhost alone is not authentication. Prevent DNS rebinding and malicious-site requests.
- Existing host-mount policy, secret-scoping, no Docker socket in workspaces, root filesystem read-only, container capabilities and TLS/CA trust are **unchanged**.
- Mutations (up/down/delete, network changes, backup/restore, agent auth) require explicit user intent, protection against duplicate requests and redacted audit logging.
- Do not create a generic `/run-command` or unauthenticated browser terminal endpoint. Avoid exposing interactive auth secrets over generic WebSocket streams.
- Prefer a single local user/session model for v1; remote/multi-user access is a different, optional threat-modelled milestone.

## PBIs and acceptance checks

### PBI-2401 — User guide, hands-on tutorials and help navigation

Delivered as an initial written draft in this documentation PR; check examples against the current CLI. Include installation, 10-minute first project, developing with Claude/Codex/Hermes/OpenCode, Herdr, Compose/Aspire, network ports, token/memory, backup, security limitations and errors. Keep one single canonical entry point (`docs/INDEX.md`).

### PBI-2402 — API & WASM architecture spike

Produce an ADR, resource ownership map, threat model, UX wireframes, packaging decision, pinned compatible packages, and a proof of concept reading project statuses only.

### PBI-2403 — Auth, local security & API contracts

Enforce origin/session validation, input validation, allowlisted operations, explicit action confirmation, redaction and bounded jobs/log streaming. Automated malicious-origin, CSRF, host-header, privilege and secret-leak tests are required.

### PBI-2404 — MVP dashboard / project control

List/create/edit/register/remove (with confirmation), start/stop/restart, inspect Compose/Aspire status, versions, diagnostics, limited logs, documentation search/help. Responsive and keyboard accessible.

### PBI-2405 — Developer workflow

Guided onboarding, agent availability and auth instructions, session start/attach, context status, token policies, loopback ports, backups; never silently start paid agent operations.

### PBI-2406 — QA, packaging and maintenance

E2E browser smoke, accessibility, Windows/Linux build and startup, REST contract tests, distribution of WASM assets with the CLI, upgrades/rollback, docs parity and M1–M8 CI non-regression.

## Not in scope (initial UI)

IDE clone, arbitrary remote shell, Docker daemon API from the browser, storing provider API keys in localStorage, hosting a public dashboard, replacing Herdr, or introducing new sandbox providers (M9).

## Sources & state

- Verified HStack CLI: `src/HermesStack.Cli/Program.cs` and associated CLI services.
- Prior security guarantees: `docs/security.md` and `docs/release-readiness.md`.
- Microsoft guidance for client-side code/secrets: https://learn.microsoft.com/aspnet/core/blazor/security/webassembly/?view=aspnetcore-10.0
- MudBlazor: https://github.com/MudBlazor/MudBlazor
- CodeBeam MudExtensions: https://github.com/CodeBeamOrg/CodeBeam.MudBlazor.Extensions

**Definition of Done**: at least one tested "first code change" walkthrough, secure loopback UI that manages a project and shows problems without needing terminal memorization, matching CLI semantics, explicit API security test evidence, CI coverage and updated release guidance.
