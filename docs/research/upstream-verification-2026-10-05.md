# Upstream verification — 2026-10-05

This note records implementation-facing findings. Versions will only be pinned in `toolchain.lock.yaml` when the corresponding milestone installs the tool.

## Herdr

Official Herdr currently installs as a single binary and manages persistent terminal sessions. Supported integrations include Claude Code, Codex, Hermes Agent and OpenCode through `herdr integration install <name>`. HermesStack should invoke those commands rather than patching agent internals.

## Codex CLI

OpenAI currently documents standalone install scripts as the primary quickstart and still supports `npm install -g @openai/codex`. Authentication state is under `$CODEX_HOME` (default `~/.codex`), with `auth.json` used by current login flows. M2 should prefer the official standalone release installer or pinned release artifact instead of assuming npm is the only official path.

## Hermes Agent

CLI install is available through the official installer. Persistent state belongs under `$HERMES_HOME` / `~/.hermes`, including config, auth, state DB, sessions and skills. Proxy/CA environment variables are supported; HermesStack should persist the dedicated project Hermes home rather than host state.

## OpenCode

OpenCode supports its official install script and package-manager installs. Global config is under `~/.config/opencode`, credentials under `~/.local/share/opencode/auth.json`; proxy variables and `NODE_EXTRA_CA_CERTS` are supported.

## RTK

RTK owns command rewrite/filtering. Its init command supports Claude, Codex, OpenCode and Hermes integrations. HermesStack should orchestrate `rtk init` modes and consume metrics rather than implement a competing filter.

## OpenViking

OpenViking exposes resources, memories and skills through `viking://` URIs, layered context retrieval and APIs/CLI. M6 mapping must use documented user/peer/resource semantics rather than invented namespaces.

## Aspire

Aspire remains an optional later orchestration backend. Current Aspire CLI and AppHost APIs must be revalidated at M8; the early abstraction in M1 prevents Compose policy lock-in.

## Claude Code

Anthropic now recommends the native installer (`curl -fsSL https://claude.ai/install.sh | bash`) and marks the npm installation as deprecated. M2 must therefore avoid baking an npm-only assumption into the workspace image. Native Claude Code trusts its bundled/system certificate stores and exposes certificate-store controls; HermesStack will continue to provide explicit Corporate CA material instead of disabling TLS.

## Caveman

The identified upstream is `JuliusBrussee/caveman`. It exposes distinct integration modes: a skill install path and a proxy/CLI path (`@caveman-ai/cli`). This is materially different from RTK's command-output rewriting model, so Caveman must remain a separate adapter with independently negotiated capabilities. Its proxy licensing is also a supply-chain/licensing consideration and must pass the upstream trust policy before enablement.

## OpenViking agent integrations

Current upstream integrations are not uniform and must stay integration-first. Claude has plugin/hooks/MCP integration paths; Codex has a dedicated plugin path; OpenCode supports a plugin/MCP path; Hermes Agent exposes OpenViking as a built-in memory provider and should use `hermes memory setup openviking` rather than a HermesStack-authored plugin. M6 will preserve those first-party mechanisms behind the context-provider adapter.

## Aspire agent-native tooling

Current Aspire tooling includes first-party agent setup (`aspire agent init`) plus CLI workflows such as start/wait/describe/logs and optional MCP support. The Aspire backend should consume these facilities for agent observability rather than creating a parallel resource/log reader. Current AppHost container APIs include bind-mount support; mandatory security parity still has to be capability-tested before the backend can be marked stable.
