# Roadmap V5

- **M1 Secure Foundation — Done**: EPIC-001..003.
- **M2 Agent Runtime — Done**: EPIC-004..008; four pinned agents, harness registry and isolated state.
- **M3 Sessions — Done**: EPIC-009; Herdr project sessions, official agent integrations, persistent restore and tmux fallback.
- **M4 Network & Security — Done**: EPIC-010..013; proxy/CA, protected secrets, security inspection and doctor.
- **M5 Token Efficiency — Done**: EPIC-014; RTK first, Caveman opt-in, policies, metrics and diagnostics.
- **M6 Shared Context — Done**: EPIC-015 / OpenViking first; project-scoped identities, fail-closed ACL sharing, context policy/budgets, agent integrations, diagnostics and memory portability.
- **M7 Operations — Done**: EPIC-016..020; transactional managed updates, recovery/portability, operational observability, CLI operations and validated multi-platform distribution.
- **M8 Aspire Experience — Done (CI/release workflow)**: EPIC-021 integrated by PR #12 and release gates closed by PR #15 (2026-10-10). Required real Aspire/OpenViking/security smoke plus Linux/Windows tests and win-x64/linux-x64/linux-arm64 SHA-256-verified artifacts passed on main CI #139 at `cf6397a6543c7e74bb231ca085136425399a9d6e`. See `docs/agile/M8-REPORT.md` and `docs/release-readiness.md`.
- **M9 Optional Sandbox Providers — Deferred**: selected EPIC-023 research; non-priority while M10 documentation/Web UI is delivered.

## Priorité produit après M8 (proposition, 2026-10-10)

La prise en main par un développeur et la documentation utilisateur deviennent la **prochaine priorité produit**, devant la recherche sur les fournisseurs de sandbox externes. La numérotation historique de M9 est conservée afin de ne pas réécrire le périmètre de l'EPIC-023.

- **M10 — Product UX, documentation & Local Web UI — Planned / Next priority** : EPIC-024, documentation utilisateur versionnée, tutoriel « premier projet / première modification », diagnostics compréhensibles et interface locale Blazor WebAssembly + MudBlazor. MudExtensions (CodeBeam.MudBlazor.Extensions) est un ajout **optionnel** après vérification de compatibilité et de valeur. L'interface est un **client** de la logique métier existante, jamais un remplacement de la CLI ; API ASP.NET Core locale, autorisation côté serveur, aucun secret dans WASM. Voir [EPIC-024](epics/EPIC-024.md), le [plan UI](../product/WEB-UI-PLAN.md) et le [guide utilisateur](../INDEX.md).
- **M9 — Optional Sandbox Providers — Deferred** : EPIC-023 reste un jalon de recherche séparé, non bloquant pour M10. Pas de changement silencieux de provider Docker/Compose/Aspire.

### Ordre de réalisation de M10

1. **M10.1 — Documentation et onboarding** : vue d'ensemble, installation, parcours « créer un projet → lancer un agent → tester → arrêter », dépannage Windows/Compose/Aspire, liens depuis le README et le tableau de bord.
2. **M10.2 — Fondations Web sécurisées** : modèle de déploiement local, API du contrôleur, session utilisateur, contrôle anti-CSRF/origine, autorisations, DTO et tests d'API ; revue des versions .NET 10/MudBlazor/MudExtensions.
3. **M10.3 — Interface quotidienne (MVP)** : tableau de bord, projets, démarrer/arrêter/redémarrer, statut en direct, agents installés, diagnostics, logs filtrés et aide contextuelle ; aucune commande arbitraire dans le navigateur.
4. **M10.4 — Parcours de développement assisté** : configuration guidée des agents, authentification via mécanisme sécurisé, lancement contrôlé des sessions, suivi du contexte/mémoire, ports et sauvegardes ; opérations sensibles confirmées et auditables.
5. **M10.5 — Distribution et validation** : build Web/CLI Windows/Linux, démarrage automatique optionnel, tests E2E navigateurs, accessibilité, sécurité localhost, régression M1–M8, documentation des limitations.

**Définition de Done** : un utilisateur novice peut installer HStack, enregistrer un projet, démarrer un environnement sûr, lancer un agent, retrouver ses diagnostics et suivre le tutoriel sans deviner les commandes. La CLI garde toutes ses capacités ; l'UI échoue de manière sûre en l'absence de backend. M10 est **planifié, non implémenté**.
