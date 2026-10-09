# HermesStack — Guide d'utilisation

HermesStack (`hstack`) pilote des espaces de travail isolés pour des agents de développement. Cette page est le **point de départ pour utiliser le produit**, pas un journal de projet. Pour l'avancement technique, voir la [roadmap](agile/ROADMAP.md).

> **État actuel :** les parcours Docker Compose et les fonctions M1–M7 sont documentés. Le parcours Aspire (M8) est intégré mais **pas encore validé pour une livraison**. Voir les [limitations et critères de release](release-readiness.md).

## 1. Avant de commencer

- Installer Docker et disposer d'un moteur Docker accessible ; le parcours par défaut utilise Docker Compose.
- Utiliser une distribution `hstack` adaptée à votre système (Windows x64, Linux x64 ou Linux ARM64). Les artefacts sont produits par la CI de `main` ; leur publication ne constitue pas à elle seule une release Aspire validée.
- Préparer un répertoire local contenant le code de votre projet. HStack ne doit pas avoir besoin de monter votre dossier personnel ou les identifiants de votre hôte.

Pour découvrir les commandes de la version installée :

```bash
hstack --version
hstack help
hstack
```

Sans argument, `hstack` affiche un tableau de bord et, dans un terminal interactif, un menu (projets, agents, sessions, mises à jour, diagnostic, sécurité, certificats et paramètres).

## 2. Démarrage rapide : un projet

Exemple Windows, à adapter à votre chemin :

```powershell
hstack init
hstack project add mascara C:\Dev\Mascara
hstack project list
hstack up mascara
hstack status mascara
hstack shell mascara
```

`init` initialise l'environnement et prépare les images lorsque Docker est disponible. `project add` enregistre un répertoire existant ; `up` démarre l'espace de travail du projet. `shell` ouvre un shell dans cet espace.

### Cycle de vie quotidien

```bash
hstack status mascara --json
hstack logs mascara
hstack restart mascara
hstack down mascara
```

Utilisez `status --json` pour les scripts, `logs` pour les problèmes d'exécution, `restart` pour redémarrer et `down` pour arrêter le conteneur. Les données durables propres au projet restent séparées des autres projets.

## 3. Travailler avec les agents et les sessions

```bash
hstack agent list
hstack agent status --project mascara
hstack herdr mascara
```

HermesStack intègre les agents Claude, Codex, Hermes et OpenCode ; les commandes `hstack claude`, `hstack codex`, `hstack hermes` et `hstack opencode` sont disponibles comme points d'entrée. Les commandes `agent`, `auth`, `session`, `herdr` et `tmux` couvrent la gestion des agents, de leur authentification et des sessions. Consultez `hstack help` et l'aide des sous-commandes de votre binaire avant de lancer des opérations sensibles.

Les identités, l'état des agents et les sessions Herdr sont isolés par projet ; n'utilisez pas directement les identifiants de l'hôte comme solution de contournement.

## 4. Réseau, sécurité et diagnostic

```bash
hstack doctor mascara
hstack security inspect mascara
hstack port add mascara 5000
hstack proxy show
hstack cert add entreprise.pem
```

- `doctor` : vérifie la configuration et les dépendances de l'espace de travail.
- `security inspect` : inspecte les protections configurées.
- `port add` : expose un port local ; les publications sont limitées à `127.0.0.1` par défaut.
- `proxy` et `cert` : configurent l'accès réseau d'entreprise et la confiance dans une autorité de certification additionnelle.

Les secrets sont protégés et ne doivent pas être placés dans les fichiers YAML ou dans les variables Compose en clair. Voir [Sécurité](security.md).

## 5. Optimisation des tokens et contexte partagé

HStack propose RTK comme optimiseur par défaut des profils sûrs/équilibrés, avec Caveman disponible sur activation explicite dans les profils appropriés. Le cumul potentiellement destructeur nécessite un consentement explicite.

Les points d'entrée `hstack token`, `hstack memory` et `hstack context` permettent de gérer l'optimisation, la mémoire OpenViking et les règles de partage de contexte. Les permissions de partage sont spécifiques au projet et doivent rester explicites. Pour le fonctionnement et les limites, consulter [Architecture](architecture.md) et [Sécurité](security.md).

## 6. Sauvegarder, migrer et mettre à jour

```bash
hstack backup mascara
hstack export environnement.hstack --include-memory
hstack update check
hstack update plan
```

- `backup` sauvegarde la configuration et l'état opérationnel, sans recopier le code source du projet.
- `export` crée un paquet portable ; la mémoire n'est incluse que si `--include-memory` est indiqué.
- Les secrets ne sont exportés que sur demande explicite avec `--include-secrets --passphrase-env <ENV>`, dans un paquet chiffré.
- `update check` et `update plan` inspectent une mise à jour gérée. `hstack update apply --yes` applique le plan avec validation et tentative de retour arrière en cas d'échec.

Avant toute mise à jour importante, conserver une sauvegarde vérifiée.

## 7. Dépannage rapide

| Symptôme | Première vérification |
| --- | --- |
| Docker indisponible | Vérifier que Docker fonctionne, puis relancer `hstack doctor mascara` |
| Projet non démarré | `hstack status mascara`, puis `hstack logs mascara` |
| Problème d'agent | `hstack agent status --project mascara` |
| Port inaccessible | Vérifier `hstack port add` et la liaison locale `127.0.0.1` |
| Proxy ou certificat d'entreprise | `hstack proxy show`, `hstack cert add` et `hstack doctor mascara` |
| Problème Aspire | `hstack doctor --aspire` ; consulter les [limitations M8](agile/M8-REPORT.md) |

Les événements applicatifs sont consignés dans `.hstack/logs/hstack.log` avec masquage des secrets. Les logs du conteneur sont consultables via `hstack logs <projet>`.

## 8. Aspire : fonctionnalité en validation

Le code M8 permet la sélection d'un orchestrateur Aspire (notamment via `hstack init --orchestrator aspire`), mais **ne constitue pas encore un parcours recommandé pour une livraison**. Le démarrage complet n'est pas couvert par la CI obligatoire. Le projet Aspire généré, l'injection de certificats et les permissions OpenViking doivent encore être validés sans réduire les protections des conteneurs.

Voir le [rapport M8](agile/M8-REPORT.md) et la [checklist de release](release-readiness.md).

## 9. Références et suivi du projet

- [Architecture et composants](architecture.md)
- [Sécurité et limites d'isolation](security.md)
- [Roadmap et avancement des jalons](agile/ROADMAP.md)
- [Rapport M7](agile/M7-REPORT.md)
- [Rapport M8](agile/M8-REPORT.md)
- [Préparation des releases](release-readiness.md)
- [README du dépôt](../README.md)

**Maintenance :** conserver ici les parcours utilisateur, les exemples et les limites connues ; consigner les dates, PR, résultats CI et décisions de livraison dans la roadmap et les rapports. Toute nouvelle fonctionnalité livrée doit être accompagnée d'un exemple d'utilisation et d'une mise à jour de ce guide.
