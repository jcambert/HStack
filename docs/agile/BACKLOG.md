# Backlog

## Done — M1 Secure Foundation

- US-001 Add a local project securely.
- US-002 Start/stop and enter an isolated Docker workspace.
- US-003 Report workspace status and initialize HermesStack idempotently.
- US-004 Register integrations and Corporate CA trust without weakening TLS.

## Done — M2 Agent Runtime

- US-010..017 Pin and integrate Claude Code, Codex, Hermes Agent and OpenCode.
- Add `IAgentHarness`, project-scoped auth/state and real Docker isolation validation.

## Done — M3 Sessions

- US-030..035 Herdr sessions, official agent integrations, persistence and tmux fallback.

## Done — M4 Network & Security

- US-040 Validate and propagate corporate proxy configuration without TLS bypasses.
- US-041 Protect secrets locally and scope release by project + agent + policy.
- US-042 Inspect declared/live workspace security and produce A/B/C/D/Critical score.
- US-043 Diagnose host/project network, certificates, agents, sessions and security.

## Done — M5 Token Efficiency

- US-1401 Register token optimizers behind a common abstraction.
- US-1402 Enable RTK for Claude Code.
- US-1403 Enable RTK for Codex, Hermes and OpenCode.
- US-1404 Prevent unsafe optimizer stacking.
- US-1405 Display evidence-qualified token gains per project.
- US-1406 Diagnose token optimizer health and privacy posture.

## Done — M6 Shared Context

- PBI-1501..1509 completed: provider abstraction, pinned OpenViking deployment, project identities, restricted shared namespaces, first-party agent integrations, context budgets, secret filtering, observability, export/import and confirmed memory clear.
- Security completion: account ACL enforcement is fail-closed, private project credentials are isolated, shared writes require an explicit restricted namespace, and root/admin credentials never enter workspaces.

## Done — M7 Operations

- EPIC-016..020 completed: managed transactional updates, backup/restore and portability, redacted operational observability, automation-friendly CLI/dashboard, and CI/CD distribution.
- US-1601/1602, US-1701/1702, US-1801, US-1901 and US-2001 completed.
- Validation completed on Linux and Windows with M3-M7 regression gates green.
- Self-contained delivery artifacts published for win-x64, linux-x64 and linux-arm64 with SHA-256 manifests.

## Done — M8 Aspire Experience

- EPIC-021 integrated by PR #12 and its Aspire release gates completed by PR #15.
- CI #138 passed the entire release candidate on Linux/Windows with required M8 runtime smoke and three SHA-256-verified packages.
- Main CI #139 (commit `cf6397a6543c7e74bb231ca085136425399a9d6e`) passed Linux M3–M8, Windows, and uploaded all three verified distribution artifacts.
- Existing installations with root-owned historical OpenViking state may require a controlled ownership migration. Linux ARM64 is package-validated in CI, not native ARM64 runtime smoke-tested.

## M9 — Optional Sandbox Providers (EPIC-023): implementation verified in PR CI #146

L'ordre M8 → M9 → M10 est **obligatoire**. L'implémentation native M9 et la recherche des options ont été présentées en PR #19 ; CI PR #146 verte, validation post-merge sur main obligatoire avant clôture formelle.

- **PBI-2301 — Done (candidate)** — Execution provider abstraction et négociation de capacités, séparées de Compose/Aspire.
- **PBI-2302 — Done (candidate)** — Native container provider reprenant Docker sans perdre les fonctionnalités M1–M8.
- **PBI-2305 — Done (candidate)** — Security parity / fail-closed et tests de non-régression avec preuves CI.
- **PBI-2303 — Research decision: No-Go adoption** — Dagger Container Use **spike/research seulement**, après validation du provider natif.
- **PBI-2304 — Research decision: No-Go adoption** — DevPod **spike/research seulement**, après validation du provider natif.

**Definition of Done M9 (code validated; main gate pending):** PBI-2301, 2302 et 2305 terminés, comparaisons PBI-2303 et 2304 documentées (Go/No-Go), revue de sécurité, tests réels Compose/Aspire/agent/OpenViking, CI Windows/Linux verte, roadmap/rapport de clôture mis à jour. Pas de nouvelle intégration externe obligatoire. Détails : [EPIC-023](epics/EPIC-023.md) et [PBI-2301](pbi/PBI-2301.md) à [PBI-2305](pbi/PBI-2305.md).

## NEXT, after green main CI — M10 Product UX, documentation & Web UI (EPIC-024), not started

- **PBI-2401** — Guide utilisateur et tutoriel : **premier brouillon publié**, validation utilisateur/CLI et aide UI encore à faire.
- PBI-2402 — Blazor WebAssembly .NET 10 + MudBlazor, MudExtensions facultatif, hôte ASP.NET Core local.
- PBI-2403 — Authentification/autorisation API locale, protections Origin/CSRF/Host et redaction des secrets.
- PBI-2404 — Dashboard, projets, Compose/Aspire, agents, logs et diagnostic.
- PBI-2405 — Parcours guidés : agents, sessions, mémoire, ports, sauvegardes.
- PBI-2406 — E2E navigateur, accessibilité, packaging Windows/Linux, compatibilité CLI.

**Statut : UI non développée.** Le candidat M9 a une CI PR verte ; ne commencer M10 qu'après fusion et CI main post-fusion verte. Détails : [EPIC-024](epics/EPIC-024.md) et [plan UI](../product/WEB-UI-PLAN.md).
