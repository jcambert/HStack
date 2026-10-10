# HermesStack — Guide utilisateur

> **Commence ici.** Ce guide est destiné aux personnes qui veulent utiliser HermesStack pour **développer un logiciel**, seules ou avec des agents IA. Le [suivi des jalons](agile/ROADMAP.md) est séparé du manuel d'utilisation.

## Qu'est-ce que HermesStack ?

HermesStack, lancé par la commande `hstack` (ou `hstack.exe` sur Windows), est un **gestionnaire local d'environnements de développement isolés**. Tu lui indiques le dossier d'un projet sur ton ordinateur. Il prépare un environnement Docker pour ce projet, avec plusieurs agents de codage possibles (Claude Code, Codex, Hermes, OpenCode), des sessions Herdr, des diagnostics et des outils optionnels de mémoire OpenViking et d'optimisation de tokens.

**Il ne remplace pas ton éditeur, Git ni un agent IA** : il orchestre ces outils et isole leurs états et leurs accès. Pour un premier essai, **Docker Compose** suffit. Aspire est une alternative avancée dont la version de CLI doit correspondre à la version épinglée par HStack.

### À quoi ça sert ?

- Développer sur un dossier de code existant sans installer chaque agent directement sur le poste.
- Lancer, inspecter, arrêter et reprendre un workspace propre au projet.
- Passer d'un agent IA à un autre sans mélanger leurs authentifications et leurs états entre projets.
- Gérer les sessions, diagnostiquer les pannes et publier localement le port d'une application en développement.
- Ajouter, si nécessaire, la mémoire OpenViking, l'optimisation des tokens RTK et des sauvegardes.

**Limite de sécurité :** un conteneur réduit l'impact d'un agent, mais n'est pas une machine virtuelle ni une garantie absolue contre du code malveillant. Le dossier du projet est monté en écriture et ses fichiers peuvent être modifiés. Toujours sauvegarder/committer les changements importants et revoir les commandes proposées par l'agent.

## Démarrage guidé : les premières commandes

Prérequis : Docker Desktop actif sur Windows (ou Docker + Compose sur Linux), le binaire HStack correspondant à ton système, et **un dossier de projet existant**. Depuis PowerShell sur Windows :

```powershell
docker version
hstack.exe help
hstack.exe init --orchestrator compose
hstack.exe project add monprojet "C:\Dev\MonProjet"
hstack.exe up monprojet
hstack.exe status monprojet
hstack.exe agent status --project monprojet
```

Adapte le chemin `C:\Dev\MonProjet` à un dossier **déjà présent sur ton PC**. `init` prépare les images ; le premier démarrage peut nécessiter un accès réseau pour récupérer les outils épinglés.

Ensuite, pour discuter avec **Codex** dans ce projet :

```powershell
hstack.exe auth codex --project monprojet
hstack.exe codex monprojet
```

L'authentification dépend de chaque fournisseur. Pour Claude, remplace `codex` par `claude`. **Si Codex indique `Permission denied (os error 13)` suivi de `--no-daemon`, lance `hstack.exe codex monprojet -- --no-daemon`**. C'est un contournement propre au serveur d'arrière-plan de Codex ; voir le [dépannage détaillé](guides/DEVELOPPER-AVEC-HSTACK.md#11-dépannage-par-symptôme).

## Pour apprendre réellement à développer avec HStack

**➡ [Tutoriel complet : développer avec HermesStack](guides/DEVELOPPER-AVEC-HSTACK.md)**

Il suit le fil concret : préparer un projet, lancer les agents, demander une modification de code, revoir les fichiers, tester le résultat, utiliser Herdr, consulter les logs et fermer proprement le workspace.

## Manuel de référence par besoin

| Je veux… | Commande ou ressource |
| --- | --- |
| Vérifier l'installation | `hstack.exe help`, `hstack.exe doctor` |
| Voir les projets | `hstack.exe project list` |
| Démarrer / arrêter | `hstack.exe up monprojet` / `hstack.exe down monprojet` |
| Voir l'état et le diagnostic | `hstack.exe status monprojet`, `hstack.exe doctor monprojet` |
| Ouvrir un terminal de développement | `hstack.exe shell monprojet` |
| Lancer Codex / Claude / Hermes / OpenCode | `hstack.exe codex monprojet`, `hstack.exe claude monprojet`, etc. |
| Vérifier les agents | `hstack.exe agent status --project monprojet` |
| Lancer / consulter Herdr | `hstack.exe session init monprojet`, `hstack.exe herdr monprojet` |
| Lire les logs | `hstack.exe logs monprojet --tail 100 --no-follow` |
| Rendre un site de développement accessible localement | `hstack.exe port add monprojet 3000` |
| Voir/activer la mémoire | `hstack.exe memory status monprojet`, `hstack.exe memory enable monprojet` |
| Vérifier l'optimisation de tokens | `hstack.exe token status monprojet` |
| Sauvegarder un projet | `hstack.exe backup monprojet` |
| Voir les versions / orchestrateurs | `hstack.exe orchestrator list`, `hstack.exe doctor --aspire` |
| Préparer une mise à jour | `hstack.exe update check`, `hstack.exe update plan` |

Les commandes avec un nom de projet doivent utiliser ton identifiant enregistré, pas nécessairement `monprojet`.

## Quand choisir Aspire ?

**Ne commence pas par Aspire si tu découvres HStack.** Compose est le backend par défaut. Aspire n'est pas un agent de codage, c'est une autre façon d'organiser les conteneurs. Il requiert le **CLI Aspire 13.6.0** prévu par `toolchain.lock.yaml`, .NET SDK 10 et Docker accessibles.

```powershell
aspire --version
hstack.exe doctor --aspire
hstack.exe init --orchestrator aspire
```

En cas d'erreur `HS2110` (par exemple Aspire 13.4.6 détecté), compare la version attendue, lance `Get-Command aspire -All` puis mets à jour **l'installation Aspire effectivement exécutée**. Ne remplace pas simplement la version attendue dans HStack. La validation de M8 a été effectuée en CI Linux et Windows, mais le démarrage graphique sur chaque PC utilisateur reste dépendant de son environnement Docker et des permissions locales.

## Quand cela ne fonctionne pas

Commence par ces trois commandes :

```powershell
hstack.exe doctor monprojet
hstack.exe status monprojet
hstack.exe logs monprojet --tail 100 --no-follow
```

Pour les cas fréquents (Docker arrêté, permissions, Codex daemon, Aspire `HS2110`, serveur Web inaccessible, mémoire), voir la [section dépannage](guides/DEVELOPPER-AVEC-HSTACK.md#11-dépannage-par-symptôme). **Ne lance pas les agents ou Docker en administrateur pour cacher une erreur de permissions.** Ne supprime pas les répertoires de jetons, caches ou secrets sans sauvegarde et diagnostic.

## Documentation existante et approfondissement

- [Tutoriel complet de développement](guides/DEVELOPPER-AVEC-HSTACK.md) — ce qu'il faut faire au quotidien.
- [Sécurité](security.md) et [modèle de menaces](security/threat-model.md) — quelles protections et quelles limites.
- [Architecture](architecture.md) — composants et flux du système.
- [Gestion de la mémoire OpenViking](context/openviking-mapping.md) — configuration et cloisonnement.
- [Préparation des livraisons](release-readiness.md) — limites testées, CI et plateformes.
- [Roadmap V5](agile/ROADMAP.md) — jalons terminés et prévus.
- [Projet M10 — interface Web locale](product/WEB-UI-PLAN.md) — proposition **non encore implémentée**.

## Une interface plus simple arrive dans la roadmap

La **CLI fonctionne aujourd'hui**. Le développement suivant est **M9** (sandbox providers et sécurité), **pas encore réalisé**. L'interface **Blazor WebAssembly + MudBlazor** est prévue **après la clôture de M9**, dans **M10** avec un hôte API sécurisé, pages projets, agents, logs, diagnostics et tutoriels interactifs. **MudExtensions est une option**, pas encore intégrée. Le navigateur ne doit jamais disposer directement des secrets ou du socket Docker. Cette future UI réutilisera la même logique métier, sans enlever les commandes utiles pour les scripts.

**Pour découvrir les commandes exactes de ton binaire, `hstack.exe help` reste prioritaire**, car la version installée peut différer du code source sur `main`.
