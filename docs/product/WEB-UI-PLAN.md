# HStack — Plan d’interface Web locale (M10)

**Statut : proposition d’architecture, pas encore développée. Le développement M10 ne commence qu'après validation et clôture de M9 (EPIC-023).** La CLI existante est le produit fonctionnel. L’UI doit offrir des chemins de moindre friction, **sans créer un deuxième moteur de gestion**.

## Ordre des jalons

M9 a été implémenté dans la PR #19 et validé par la CI PR #146 ; les options Dagger/DevPod ont reçu une décision No-Go d'adoption (études documentaires). La prochaine étape **après fusion et CI main verte** est M10. Le présent document **préserve le plan M10 sans autoriser son implémentation anticipée**. Le guide utilisateur existant continue à être amélioré au fil des corrections. Voir [Roadmap](../agile/ROADMAP.md) et [EPIC-023](../agile/epics/EPIC-023.md).

## Parcours à simplifier

| Besoin de l’utilisateur | CLI actuelle | Future page UI |
| --- | --- | --- |
| Vérifier que HStack fonctionne | `hstack doctor`, `hstack orchestrator list` | Accueil / état des prérequis |
| Enregistrer un dossier source | `hstack project add` | Assistant « Ajouter un projet » |
| Démarrer et arrêter | `hstack up/down/restart` | Fiche projet / actions explicites |
| Choisir Compose / Aspire | `hstack orchestrator`, `hstack plan` | Environnement / capacités et diagnostics |
| Vérifier les agents | `hstack agent status` | Panneau « Agents » |
| S’authentifier auprès d’un agent | `hstack auth` | Assistant avec contrôle sécurisé du flux |
| Développer avec un agent | `hstack claude/codex/hermes/opencode` | Démarrage de sessions encadrées, état et lien vers terminal |
| Consulter la mémoire | `hstack memory status` | Mémoire, périmètres, confidentialité |
| Consulter les erreurs | `hstack logs`, `hstack doctor` | Diagnostics compréhensibles et journaux filtrés |
| Lire un tutoriel | `docs/INDEX.md` | Aide et tutoriels contextuels |

## Architecture cible

~~~text
Navigateur sur le même PC
  └── HermesStack.Web.Client (.NET 10 Blazor WASM + MudBlazor)
        └── API HTTPS/localhost même origine, avec session locale
              └── HermesStack.Web.Host (ASP.NET Core)
                    ├── Application services / domain / authorisation
                    ├── WorkspaceOrchestrator -> Compose | Aspire
                    ├── Agents / sessions / tokens / OpenViking
                    └── Secret store (strictement côté hôte, jamais WASM)
~~~

MudExtensions (CodeBeam.MudBlazor.Extensions) est **une option de composants**, pas une dépendance obligatoire. Tester la combinaison .NET 10 + MudBlazor 9 + MudExtensions 9 avant de figer les packages. Réduire le JavaScript supplémentaire ; éviter une dépendance UI lourde inutile.

## Sécurité : conception avant les écrans

- `127.0.0.1` uniquement par défaut, même origine pour WASM et API ; IPv6 loopback si explicitement pris en charge.
- **Aucune autorité accordée sur la base de « localhost » seul**. Une page externe peut tenter des requêtes vers le serveur local ; imposer une session/jeton anti-CSRF adapté, contrôle d’Origin, de Host et des autorisations **côté serveur**. Cookies HttpOnly, Secure lorsque HTTPS, SameSite ; politique CORS explicite.
- Aucun jeton de fournisseur, secret projet, accès Docker ou état d’authentification durable sensible transmis au client WASM. Côté client, ne conserver que les DTO d’affichage.
- API par actions explicites et paramètres validés ; ne pas créer d’endpoint générique qui exécute des chaînes arbitraires. Réponses rédigées sans secrets.
- Mutations idempotentes ou protégées contre doubles clics ; opérations longues suivies par ID et annulation contrôlée ; logs avec limite de taille, redaction et pagination.
- Garder les règles existantes : workspace non-root, read-only, capabilities dropped, accès réseau local et confiance CA/TLS non dégradée.

## Version MVP (M10.3)

Navigation initiale : **Accueil · Projets · Agents · Diagnostics · Journaux · Documentation · Paramètres**.

Écrans priorisés :

1. Assistant de premier lancement : Docker, Compose/Aspire, version Aspire épinglée, images nécessaires, messages d’erreur traduits en actions concrètes.
2. Liste et détails des projets : chemins locaux visibles, états de containers, backend, démarrer/arrêter, accès aux journaux.
3. Agents : présence/versions, indication de connexion, démarrer une session contrôlée (auth confidentielle gérée hors WASM).
4. Diagnostics : checks avec recommandations pratiques et copie de commandes uniquement lorsque nécessaire.
5. Documentation : tutoriels Markdown par tâche et recherche basique, en miroir de `docs/INDEX.md`.
6. Réglages sécurisés : préférences non sensibles et vue des politiques, sans éditer directement des secrets.

## Évolutions (M10.4+)

Sessions Herdr, workflows de développement guidés, mémoire, optimisations RTK, sauvegardes, restauration avec confirmation, ports, mises à jour contrôlées, état en temps réel si les usages le justifient. Un terminal intégré ne fait **pas** partie du MVP.

## Tests d’acceptation

- Première installation Windows et Linux : API et client démarrent localement ; aucun port ouvert au LAN sans consentement.
- Projet ajouté via UI → apparaît dans la CLI ; actions UI/CLI sont cohérentes, sans divergence d’état.
- Lancement Compose puis Aspire (si dépendances disponibles), diagnostics identiques, échecs clairs.
- Navigateur externe/origine non autorisée ne peut pas appeler l’API ; tests CSRF, Host header, traversal, permissions.
- Pas de secret dans JS, localStorage, trafic de l’API, logs ou captures de diagnostic.
- Tests navigateur automatisés : navigation, erreurs, clavier, accessibilité et écrans mobiles.
- CI Linux/Windows et régressions M1–M8 inchangées ; publication des fichiers statiques WASM testée.
- Un utilisateur suit le guide « Première contribution avec un agent » et obtient une modification et une commande de test démontrables.

Voir [EPIC-024](../agile/epics/EPIC-024.md), [Guide utilisateur](../INDEX.md) et [Démarrer un projet de développement](../guides/DEVELOPPER-AVEC-HSTACK.md).
