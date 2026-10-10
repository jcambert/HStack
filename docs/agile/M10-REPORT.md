# M10 — User documentation & local Blazor Web UI (2026-10-10)

**State:** Done — [PR #20](https://github.com/jcambert/HStack/pull/20) merged, [CI candidate #158](https://github.com/jcambert/HStack/actions/runs/38084113404) and [CI post-merge #159](https://github.com/jcambert/HStack/actions/runs/38084607822) each 5/5 green. Tested `main` code SHA: `91f6f31c9da641ecb8cc5f9dac0ec7f5722a1bb9`.

## Scope delivered

- `hstack ui [--port ...] [--no-browser]`: on-demand local dashboard, with no changes to regular CLI commands or project YAML schema.
- `HermesStack.Web.Client` (.NET 10 Blazor WebAssembly + pinned MudBlazor 9.11.0), `HermesStack.Web.Host` (ASP.NET Core) and typed shared `HermesStack.Web.Contracts`.
- HTTPS `127.0.0.1` only, ephemeral self-signed certificate, one-use URL fragment, HttpOnly Secure SameSite Strict session, strict Host/Origin/anti-CSRF protections on mutations, bounded redacted logs and explicit confirmations.
- Projects registration/list/status/edit/delete, start/stop/restart, Compose/Aspire diagnostics, agent availability, terminal instructions for safe agent authentication and sessions, memory status, port configuration, backups and embedded searchable Markdown guide.
- The host delegates to the existing application services, Compose/Aspire orchestrators, M9 native provider and project state. No second Docker daemon controller, no arbitrary command route, no agent secrets in WASM.

## Tested acceptance evidence

| Gate | Verified result / limits |
| --- | --- |
| Linux build/unit/integration | Success, required in CI #158 and main #159 |
| Windows build/unit | Success, required in CI #158 and main #159 |
| Web API/security | Loopback HTTPS, unauthenticated access denied, malicious Host/Origin, CSRF, bootstrap replay, project/CLI storage parity; success on Linux |
| Actual browser | Chrome headless loads and renders authenticated MudBlazor dashboard on Linux (with bounded retries and strict visible-DOM assertions) |
| Existing regressions | M3–M9 Compose/Aspire/agent/Herdr/OpenViking/token/security/operations Linux runtime gates green |
| Delivery | `win-x64`, `linux-x64`, `linux-arm64` Actions artifacts, bundled `web/` host and SPA/manual assets, checksum verification green |

## Explicit limitations

- Windows Chrome/Edge runtime and native ARM64 Web UI runtime have **not** been exercised by the hosted runner; Windows is build/unit-validated and ARM64 is packaged, not native-certified.
- A separate hands-on Windows first-project walkthrough, full WCAG accessibility audit, and interactive end-user trial are desirable future quality work, not CI-passed claims.
- Local first-launch HTTPS certificate is **self-signed**, so browsers may show a trust warning. Do not turn off CA/TLS validation for Docker, agent or corporate proxies.
- No remote/LAN/multi-user UI, Web terminal, interactive agent authentication tokens in the browser, or public Docker control endpoint. Auth/session work remains available through existing terminal commands.
- MudExtensions was evaluated as an **optional** component but not installed because M10 does not require an extension-specific control.
- Artifacts are downloadable GitHub Actions outputs; **no tagged GitHub Release** is implied.

## Regression and compatibility

Existing M1–M9 code paths, pinned Docker/agents, M9 native provider, OpenViking data and secret handling are retained. The UI is started only when requested; older CLI workflows do not start the Web host. The shipping bundle retains the root `hstack` binary and places `hstack-web` plus client assets under `web/`.

See [user guide](../product/WEB-UI-USER-GUIDE.md), [security ADR](adr/ADR-024.md), [roadmap](ROADMAP.md) and [main workflow](https://github.com/jcambert/HStack/actions/runs/38084607822).
