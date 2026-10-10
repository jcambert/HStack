# Roadmap V5

- **M1 Secure Foundation — Done**: EPIC-001..003.
- **M2 Agent Runtime — Done**: EPIC-004..008; four pinned agents, harness registry and isolated state.
- **M3 Sessions — Done**: EPIC-009; Herdr project sessions, official agent integrations, persistent restore and tmux fallback.
- **M4 Network & Security — Done**: EPIC-010..013; proxy/CA, protected secrets, security inspection and doctor.
- **M5 Token Efficiency — Done**: EPIC-014; RTK first, Caveman opt-in, policies, metrics and diagnostics.
- **M6 Shared Context — Done**: EPIC-015 / OpenViking first; project-scoped identities, fail-closed ACL sharing, context policy/budgets, agent integrations, diagnostics and memory portability.
- **M7 Operations — Done**: EPIC-016..020; transactional managed updates, recovery/portability, operational observability, CLI operations and validated multi-platform distribution.
- **M8 Aspire Experience — Done (CI/release workflow)**: EPIC-021 integrated by PR #12 and release gates closed by PR #15 (2026-10-10). Required real Aspire/OpenViking/security smoke plus Linux/Windows tests and win-x64/linux-x64/linux-arm64 SHA-256-verified artifacts passed on main CI #139 at `cf6397a6543c7e74bb231ca085136425399a9d6e`. See `docs/agile/M8-REPORT.md` and `docs/release-readiness.md`.
- **M9 Optional Sandbox Providers — Done (2026-10-10; main CI #148)**: EPIC-023. Stabiliser l'abstraction des execution providers et le backend Docker natif, vérifier la parité de sécurité puis évaluer Dagger Container Use et DevPod par spikes. Une étude n'est pas une intégration obligatoire. Voir [EPIC-023](epics/EPIC-023.md).
- **M10 Product UX & Local Web UI — Planned / NEXT**: EPIC-024. Interface locale Blazor WebAssembly (.NET 10) + MudBlazor, MudExtensions optionnel, API ASP.NET Core sécurisée et aide utilisateur. La **première version écrite** du guide utilisateur est déjà publiée; aucun développement Web UI n'a commencé. Voir [EPIC-024](epics/EPIC-024.md), [plan UI](../product/WEB-UI-PLAN.md) et [guide](../INDEX.md).

## Ordre impératif après M8 (décision 2026-10-10)

**M8 terminé → M9 terminé (CI #148) → M10 à développer.**

On ne saute pas un jalon pour commencer le suivant. Les corrections de sécurité, documentation utilisateur et incidents de production peuvent toujours être prises en charge sans démarrer une fonctionnalité M10 avant la clôture de M9. L'UI est reportée après M9, pas annulée.

### M9 — Execution environments et sandbox providers (EPIC-023)

1. **PBI-2301** — Concevoir `IExecutionEnvironmentProvider`, la séparation orchestrateur / provider et la négociation des capacités.
2. **PBI-2302** — Adapter Docker native containers au contrat, sans régression Compose/Aspire, avec test de cycle de vie.
3. **PBI-2305** — Vérifier la parité de sécurité réelle des providers : `non-root`, `read-only`, `cap-drop=ALL`, `no-new-privileges`, pas de Docker socket, mounts privés, ports loopback, CA/TLS intact et fail-closed.
4. **PBI-2303** — *Spike* Dagger Container Use **après stabilisation du backend natif** : compatibilité, sécurité, intégrations, coûts et décision Go/No-Go.
5. **PBI-2304** — *Spike* DevPod **après stabilisation du backend natif** : DevContainer, isolation, montages, persistance et décision Go/No-Go.

**Gate M9 → M10** : abstraction et provider natif livrés/testés, preuves de non-régression des parcours Compose/Aspire/agents/OpenViking, matrice de sécurité et capacités, spikes documentés (ou décisions formelles motivées), CI Windows/Linux verte et rapport de clôture M9. L'absence de nouveau provider externe est acceptable si les spikes concluent « No-Go ». M9 a son **candidat d'implémentation** validé en CI PR #146 (5/5) sur `fce766dc0015642eab0f31b24322c5f6b21c4b5d` : [exécution](https://github.com/jcambert/HStack/actions/runs/38075662535). La clôture formelle est maintenant acquise : [CI main #148](https://github.com/jcambert/HStack/actions/runs/38076623826) 5/5 verte sur `a3b88c6b9aa02e3fa538fa27cc31564711e84d6e`. Dagger et DevPod sont des **No-Go d'adoption** avec études documentaires ; aucun PoC externe exécuté ni provider externe activé.

### M10 — Documentation et interface utilisateur locale (EPIC-024)

- **M10.1 — Documentation** : guide utilisateur et tutoriel initial publiés dans `docs/`; vérifications terrain et intégration à l'UI restent à effectuer.
- **M10.2 — Fondations Web sécurisées** : contrat API hôte, session et protections Origin/CSRF/Host, Blazor WebAssembly + MudBlazor, évaluation optionnelle MudExtensions.
- **M10.3 — UI quotidienne** : projets, statuts, orchestration Compose/Aspire, agents, logs et diagnostic.
- **M10.4 — Développement assisté** : onboarding, agents et sessions, mémoire, ports, sauvegardes.
- **M10.5 — Tests/distribution** : E2E navigateur, accessibilité, builds et packaging, CI/régressions.

**Gate M10 — SATISFAIT :** M9 fusionné et CI main #148 verte. L'UI M10 reste à développer. Préserver tous les invariants de sécurité et la CLI comme interface de référence. Ne pas annoncer M10 comme terminé sur la seule présence d'un guide.

**Validation détaillée :** [Rapport M9](M9-REPORT.md) et [EPIC-023](epics/EPIC-023.md). Ne pas confondre recherche sans PoC externe et certification des sandbox tiers.
