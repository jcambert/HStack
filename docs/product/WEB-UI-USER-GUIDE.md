# Interface locale HermesStack (M10)

> **Préversion M10 en validation :** ce guide décrit le nouvel exécutable du candidat PR M10. Il ne devient une fonctionnalité officiellement livrée qu'après CI et fusion dans `main`.

## Pourquoi utiliser l'interface ?

HermesStack prépare des environnements isolés pour ton code et tes agents (Claude, Codex, Hermes, OpenCode). L'interface te permet de voir les projets, les démarrer/arrêter, inspecter les agents, les ports et les diagnostics, puis suivre le guide intégré sans devoir mémoriser toute la CLI. **La CLI reste disponible et utilise exactement les mêmes fichiers de configuration.**

## Installer

Télécharge l'archive **complète** `hstack-win-x64`, `hstack-linux-x64` ou `hstack-linux-arm64` à partir de la CI validée ou d'une distribution future. **Conserve le sous-dossier `web/` à côté de `hstack.exe` ou `hstack`** : il contient l'hôte ASP.NET Core et les fichiers Blazor WASM. Ne déplace pas séparément le binaire Web sans ses ressources.

Prépare Docker et le workspace selon [le guide de démarrage](../INDEX.md). Dans PowerShell :

```powershell
hstack.exe init --orchestrator compose
hstack.exe ui
```

Sous Linux : `./hstack ui`. Le navigateur est normalement ouvert automatiquement. Sinon, **copie le lien à usage unique** affiché dans le terminal. L'interface écoute sur `https://127.0.0.1:5158` uniquement (autre port : `hstack ui --port 5159`).

Au lancement, HermesStack produit **un certificat HTTPS temporaire et autosigné**. Le navigateur peut afficher un avertissement de confiance pour **cette adresse locale uniquement**. Vérifie que l'adresse indiquée est bien `127.0.0.1`, puis choisis explicitement de continuer si tu fais confiance à ton propre processus HStack. Ne désactive pas les vérifications TLS des agents, de Docker ou de ton système. Une solution de certificat local approuvé pourra être ajoutée plus tard.

Le lien contient un fragment `#...` à usage unique, non envoyé dans les requêtes HTTP. Il établit une session protégée par cookie HttpOnly/Secure/SameSite. Si tu fermes le serveur ou ouvres un nouvel appareil, **relance `hstack ui`** pour un nouveau lien. Aucun accès LAN n'est autorisé, même avec un `ASPNETCORE_URLS` défini.

## Utiliser les pages

**Projets** : ajoute le chemin absolu d'un dossier existant, consulte les projets enregistrés et leur état Compose/Aspire, puis démarre, arrête ou redémarre avec confirmation. Supprimer un enregistrement n'efface **jamais le répertoire source**.

**Développer** : sélectionne un projet et inspecte les agents. Pour les connexions sensibles (authentification de Claude, Codex, Hermes ou OpenCode) et les sessions interactives Herdr, l'UI **explique la commande CLI à exécuter dans ton terminal** au lieu d'exposer les jetons à du JavaScript ou à un WebSocket arbitraire.

**Paramètres projet** : change le nom/chemin/backend, gère les ports `127.0.0.1` et sauvegarde l'état. Arrête impérativement le workspace avant de changer sa configuration. Le backend Aspire exige son CLI épinglé.

**Diagnostics** : affiche les prérequis Compose et Aspire, leurs versions/raisons d'indisponibilité et les commandes de dépannage.

**Guide utilisateur** : consulte le Markdown d'origine (source de vérité dans le dépôt) et filtre les lignes du guide.

Le tableau de bord se rafraîchit toutes les trente secondes ; l'API et le navigateur ne détiennent jamais d'autorité directe sur Docker.

## Sécurité et limites

- **Utilisation locale par un seul utilisateur**, via une URL HTTPS sur `127.0.0.1`. Ne transmets jamais le fragment de démarrage à quelqu'un.
- Pas de service Web distant, tunnel public ni interface d'administration multi-utilisateur.
- Pas de shell de navigateur, pas d'authentification agent interactive dans le WASM, pas de clé API stockée dans `localStorage`, pas de Docker socket accessible au client.
- Création, configuration et actions utilisent directement les services HStack, gardant les contrôles de montage et les invariants M1–M9.
- Les logs sont bornés à 200 lignes et 64 KiB, avec masquage automatique des formats de clés courants. Les développeurs doivent éviter d'imprimer des secrets arbitraires dans les journaux.
- L'interface **ne remplace pas** les sauvegardes Git ni l'examen des modifications faites par les agents.
- La vérification runtime Docker/Aspire de CI est sur Linux x64 ; Windows est validé par compilation/tests et par vérification des packages, pas par un navigateur Windows en situation réelle.

## Développer l'interface depuis le dépôt

```powershell
dotnet restore HermesStack.slnx
dotnet run --project src/HermesStack.Web.Host -- --no-browser
```

Le serveur utilise `HSTACK_HOME` comme la CLI, le `toolchain.lock.yaml` épinglé et les assets du dépôt. L'hôte demande une session à usage unique ; le client WASM appelle des endpoints REST typés, tous limités aux opérations autorisées. Le développement est dans `src/HermesStack.Web.Client`, `src/HermesStack.Web.Host` et `src/HermesStack.Web.Contracts`.

Pour un premier code modifié avec un agent, suis le [tutoriel de développement](../guides/DEVELOPPER-AVEC-HSTACK.md).
