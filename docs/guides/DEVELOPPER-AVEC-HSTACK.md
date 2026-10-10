# Développer avec HermesStack — de zéro à une première modification

> **Guide utilisateur — disponible aujourd’hui en CLI.** L’interface Web Blazor est **prévue pour M10**, elle n’est **pas encore développée**. Les commandes ci-dessous proviennent de la CLI actuelle dans `src/HermesStack.Cli/Program.cs`.

## 1. C’est quoi, concrètement ?

HermesStack (commande `hstack` ou `hstack.exe` sous Windows) est un **gestionnaire d’environnements de développement pour des agents IA**. Tu lui indiques un dossier de code local. Il prépare un conteneur Docker isolé, donne accès au projet à l’intérieur du conteneur (`/workspace`) et permet d’utiliser Claude Code, Codex, Hermes ou OpenCode sur ce code.

**Ce que HStack fait** : gérer les projets, lancer/arrêter l’environnement, séparer les données et authentifications des agents par projet, inspecter la sécurité, consulter des logs, superviser des sessions Herdr, utiliser OpenViking (optionnel) et optimiser certains flux de tokens.

**Ce que HStack ne fait pas** : créer automatiquement l’application métier à ta place, fournir une IDE graphique aujourd’hui, remplacer l’abonnement/identification de chaque agent, ni garantir que toutes les commandes installables sur l’hôte sont présentes dans le conteneur.

Deux termes à retenir :
- **Projet** : un dossier **déjà présent sur le PC**, par exemple `C:\Dev\MaBoutique`. C’est ton code.
- **Workspace** : le conteneur sécurisé correspondant à un seul projet. Le chemin du code **dans le conteneur** est `/workspace`. Les changements sur les fichiers du projet sont visibles sur le PC.
- **Orchestrateur** : **Compose**, conseillé pour commencer, ou **Aspire** (plus avancé, version du CLI à aligner avec le verrou HStack).

## 2. Avant la première utilisation

1. Installer Docker Desktop sur Windows, le lancer et vérifier que son moteur répond ; sur Linux, installer Docker et le plugin Compose.
2. Choisir le bon binaire HStack pour ton système et l’ajouter au `PATH` ou lancer `.\hstack.exe` depuis son dossier.
3. Vérifier les commandes dans PowerShell :

~~~powershell
docker version
docker compose version
hstack.exe --version
hstack.exe help
~~~

**Important :** la première initialisation construit des images Docker, télécharge les outils épinglés et peut nécessiter un accès réseau (proxy ou CA d’entreprise éventuellement). Garde suffisamment d’espace disque.

## 3. Parcours de démarrage sur Windows (Compose)

Supposons que le code est déjà dans `C:\Dev\MaBoutique`. Dans PowerShell :

~~~powershell
Test-Path "C:\Dev\MaBoutique"          # doit renvoyer True
hstack.exe init --orchestrator compose
hstack.exe project add maboutique "C:\Dev\MaBoutique"
hstack.exe project list
hstack.exe up maboutique
hstack.exe status maboutique
hstack.exe agent status --project maboutique
~~~

Si tu n’as pas encore de dossier, crée-le avec `New-Item -ItemType Directory -Force "C:\Dev\MaBoutique"` ou clone ton dépôt Git habituel avant `project add`.

**Résultat attendu** : le projet est enregistré ; son workspace est `Running` et les agents installés sont listés. Si quelque chose échoue, lance `hstack.exe doctor maboutique`. Ne force pas les droits administrateur pour contourner une erreur de volume ou de secret.

## 4. Première session : développer avec un agent

La commande qui lance l’agent est explicite et associe toujours l’agent **au projet**. Exemple avec Claude Code :

~~~powershell
hstack.exe auth claude --project maboutique
hstack.exe claude maboutique
~~~

Tu peux utiliser alternativement Codex, Hermes ou OpenCode (chacun peut avoir sa propre procédure d’authentification) :

~~~powershell
hstack.exe auth codex --project maboutique
hstack.exe codex maboutique

# Alternatives :
hstack.exe hermes maboutique
hstack.exe opencode maboutique
~~~

Tu te retrouves dans l’interface **de l’agent choisi**, pas dans une UI graphique HStack. Donne une instruction claire et vérifiable, par exemple :

> « Lis le README et la structure du projet. Explique-moi comment lancer les tests. Propose une petite amélioration, montre les fichiers modifiés, puis exécute les tests disponibles. Ne change pas d’autres fichiers sans m’expliquer pourquoi. »

**Vérifie le résultat** après la session : inspecte les modifications avec ton éditeur habituel et `git diff`, lance les tests pertinents pour le projet, puis crée ton commit Git. Les tests à lancer dépendent de ton projet (`npm test`, `pytest`, `dotnet test`, etc.) et doivent **être installés dans l’environnement**, pas supposés présents.

## 5. Développer sans agent : shell et fichiers

Tu peux simplement utiliser le workspace comme terminal de développement :

~~~powershell
hstack.exe shell maboutique
~~~

Tu arrives **à l’intérieur du conteneur Linux** :

~~~bash
pwd                 # /workspace
ls -la
git status
node --version
# Lancer ensuite les commandes adaptées à TON dépôt.
~~~

Les modifications des fichiers dans `/workspace` se retrouvent dans `C:\Dev\MaBoutique`. Sors du shell avec `exit`, puis poursuis l’édition dans Visual Studio Code / ton IDE habituel, directement sur le dossier Windows.

## 6. Sessions persistantes : Herdr

Si tu alternes entre plusieurs agents ou veux suivre leur état :

~~~powershell
hstack.exe session init maboutique
hstack.exe session status maboutique
hstack.exe session agents maboutique
hstack.exe herdr maboutique
~~~

`session init` prépare la session par projet. `herdr` ouvre le gestionnaire de sessions dans le workspace ; ce n’est pas un serveur Web. Pour une utilisation simple, `hstack.exe claude maboutique` ou `codex` suffit.

## 7. Développer une application Web et voir son résultat

Si **ton application** écoute sur le port **3000 dans le conteneur** :

~~~powershell
hstack.exe port add maboutique 3000
hstack.exe port list maboutique
hstack.exe restart maboutique
~~~

Démarre ensuite ton serveur de développement **dans** le workspace (commande dépendant de ton projet). Il doit écouter sur une interface accessible au réseau du conteneur (souvent `0.0.0.0` **à l’intérieur du conteneur**). Depuis le PC, ouvre `http://127.0.0.1:3000` si le port hôte est bien 3000 ; sinon utilise le port retourné par `port list`.

**Ne publie pas le port vers le réseau local** sans l’avoir décidé : HStack utilise le loopback `127.0.0.1` par défaut.

## 8. Mémoire, tokens et diagnostic : seulement quand utiles

Commence sans mémoire externe si tu veux limiter les dépendances. Après avoir réussi ton premier projet, tu peux l’activer :

~~~powershell
hstack.exe memory status maboutique
hstack.exe memory enable maboutique
hstack.exe memory doctor maboutique
hstack.exe token status maboutique
hstack.exe token doctor maboutique
~~~

Le service OpenViking peut nécessiter une image Docker, des permissions de fichiers et un démarrage propres. Le stockage est cloisonné par projet ; le partage entre projets doit rester explicite. N’envoie jamais de secrets dans une demande d’agent ni un document de mémoire.

## 9. Choisir Aspire (facultatif)

Compose est **suffisant pour débuter**. Aspire est un autre orchestrateur, pas un autre agent. Pour le sélectionner dès l’installation :

~~~powershell
aspire --version
hstack.exe doctor --aspire
hstack.exe init --orchestrator aspire
~~~

HStack vérifie strictement la version Aspire CLI **13.6.0** (pin actuel du dépôt). Si `HS2110` indique « detected 13.4.6 », HStack **ne va pas changer cette version automatiquement**. Si Aspire est un outil .NET global, mets-le à jour via `dotnet tool update --global Aspire.Cli --version 13.6.0` ; sinon vérifie quelle installation `Get-Command aspire -All` trouve et utilise la procédure de mise à jour correspondant à cette installation. Ferme puis rouvre PowerShell, et recommence.

**Ne saisis pas** `aspire16` dans `--orchestrator` : les valeurs valides sont `compose` et `aspire`.

Pour voir le plan ou vérifier le backend actuel :

~~~powershell
hstack.exe orchestrator list
hstack.exe plan maboutique --orchestrator aspire
hstack.exe aspire status maboutique
~~~

Une transition d’orchestrateur exige l’arrêt du projet ; la documentation technique décrit le comportement en détail.

## 10. Cycle de travail quotidien (mémo)

~~~powershell
hstack.exe status maboutique
hstack.exe up maboutique
hstack.exe agent status --project maboutique
hstack.exe claude maboutique             # ou codex / hermes / opencode
hstack.exe logs maboutique --tail 100 --no-follow
hstack.exe doctor maboutique
hstack.exe down maboutique
~~~

`down` arrête le conteneur ; cela **ne supprime pas** ton dépôt Git local ni les données durables propres à ton projet. Pour comprendre ou sauvegarder l’état de HStack : `hstack.exe backup maboutique`. Vérifie tes modifications Git avant une mise à jour d'image ou une réinstallation.

## 11. Dépannage par symptôme

| Symptôme | Vérification et action |
| --- | --- |
| `docker` ne répond pas | Démarrer Docker Desktop ; réessayer `docker version` |
| `HS2110` Aspire version incorrecte | `aspire --version`, `Get-Command aspire -All` ; aligner la version CLI sur `13.6.0` |
| Codex : `Permission denied (os error 13)`, puis demande `--no-daemon` | Contournement : `hstack.exe codex maboutique -- --no-daemon`. L'option après `--` va à **Codex**, pas à HStack ; le problème concerne son serveur de fond. Si cela échoue aussi : `hstack.exe shell maboutique`, `codex --version`, `id`, `echo "$CODEX_HOME"`, `ls -ld "$CODEX_HOME" "$HOME/.codex"`. Ne pas utiliser `chmod -R 777`, lancer root, supprimer des jetons ou utiliser Docker Debug pour contourner l'isolation. Voir les [signalements Codex](https://github.com/openai/codex/issues/48999). |
| `project add` échoue | Vérifier que le dossier **existe** et que son chemin est autorisé ; `hstack.exe config validate` |
| `up` échoue | `hstack.exe doctor maboutique` et `hstack.exe logs maboutique --tail 100 --no-follow` |
| Un agent n’est pas connecté | `hstack.exe agent status --project maboutique` puis `hstack.exe auth <agent> --project maboutique` |
| Impossible d’accéder à un serveur Web | Vérifier le port dans `hstack.exe port list maboutique` et l’adresse d’écoute du serveur dans le conteneur |
| OpenViking ne démarre pas | `hstack.exe memory doctor maboutique` ; vérifier les permissions sans élargir l’accès aux secrets |
| Nouvelle commande absente | Vérifier le `hstack.exe help` **de la version installée**, qui peut différer du dépôt en développement |

## Suite du guide

- [Accueil du guide](../INDEX.md) : navigation, sécurité, projets et opérations.
- [Sécurité](../security.md) : limites de l’isolation et des secrets.
- [Roadmap](../agile/ROADMAP.md) : ce qui est livré versus prévu.
- [Plan de l’interface Web (M10)](../product/WEB-UI-PLAN.md) : futur parcours plus ergonomique.

**Limite de ce tutoriel :** les commandes ont été comparées au code de la CLI, mais ces exemples pédagogiques n’ont pas fait l’objet d’un test Windows interactif dans cette PR de documentation ; l’ajout de scénarios E2E pour les tutoriels fait partie de M10.
