<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/mue-banniere-sombre.png">
  <img src="docs/images/mue-banniere-clair.png" alt="Mue — Self-updates for .NET desktop apps" width="640">
</picture>

# Mue

**Français** · [English](README.md)

Mise à jour automatique pour applications de bureau .NET : rechercher, télécharger, fermer ce qui bloque, installer.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/01-update-flow-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/01-update-flow-light.gif" alt="Démonstration : l'application trouve une nouvelle version, la fenêtre de mise à jour affiche les notes, télécharge, puis redémarre sur la nouvelle version" width="960">
</picture>

## Pourquoi Mue

Mue est le moteur de mise à jour de [DockPad](https://github.com/syl-craft/DockPad), sorti en bibliothèque une fois éprouvé en production. Il sert aujourd'hui à :

- DockPad (WPF)
- PicaLite (WPF)
- AlterVNC (Avalonia, Windows et macOS)

Le cœur ignore votre interface et votre source de mises à jour. Vous branchez vos textes, votre journal, votre planification et votre thème. Rien ne s'installe seul : c'est toujours l'utilisateur qui lance l'installation.

## Fonctionnalités

- **Backend Velopack** pour les releases GitHub, sur Velopack 1.2.161 exactement.
- **Recherche planifiée** : première recherche 20 secondes après le démarrage, puis permission et préférence réévaluées toutes les 15 minutes, avec au moins six heures entre deux recherches. Elles s'annulent à l'arrêt de l'application ou si l'option automatique est coupée.
- **Téléchargement sans installation** : un téléchargement n'applique jamais la mise à jour tout seul.
- **Pastille de version** : un contrôle `UpdateBadge` autonome affiche la version actuelle et la nouvelle (`v1.2.0 → 1.3.0`).
- **Fenêtre de mise à jour** avec notes de version (Markdown), progression et état.
- **Processus bloquants** : Mue liste les programmes qui utilisent les fichiers de l'application, laisse choisir ceux à fermer et revérifie avant d'installer. Windows passe par le Restart Manager ; macOS 15 ou plus récent.
- **Comportement à l'arrêt** selon le backend : `Refuse`, `AskToQuit` ou `Kill`. Le backend Velopack prend `AskToQuit` sur macOS et `Kill` ailleurs.
- **Marqueur en attente** : `VerifyLaunch()` indique au démarrage suivant si la mise à jour a réussi ou été annulée.
- **Textes français et anglais** intégrés, avec repli clé par clé si l'application n'en surcharge qu'une partie.
- **Clés de style** : les fenêtres reprennent les brosses et styles de boutons de l'application, avec des valeurs par défaut pour une application sans thème.
- **`LatestReleaseOnly`** : interroger seulement `/releases/latest` sur GitHub, sans repli vers une autre release.

### Nouveautés de la 0.3.0

- **Accord explicite** : quand l'hôte annonce les effets de l'installation (`Summary`), la fenêtre demande toujours confirmation avant `PrepareAsync`.
- **Programmes ignorés** (`AskToQuit` et `Kill`) : décocher un groupe le laisse à l'installeur. **Installer sans les fermer** donne cet accord directement. Un programme apparu entre-temps bloque toujours.
- **Identités illisibles** : un exécutable retenu dont l'identité ne se lit pas reste listé comme `Unavailable` et le scan est `Incomplete`, avec un avertissement dans la fenêtre.
- **Détection sous macOS** alignée sur le contrat Windows : plusieurs chemins, processus courant facultatif, cinq résultats de fermeture, annulation.
- **Textes génériques** : `UpdateTexts.AppName` remplace `{AppName}` dans les textes intégrés.
- **Mue.Avalonia** : fenêtres et pastille pour Avalonia 12.

### Nouveautés de la 0.5.0

- **Ligne de commande** pour les scripts et les agents IA : `UpdateCommand` (`--check`, `--yes`, `--close-blockers`, `--restart`, `--json`), codes de sortie, installation sans fenêtre. Première installation : `Setup.exe --silent`.

### Nouveautés de la 0.4.0

- **Clés de texte** : `UpdateTextKeys.All` et `Missing(...)` ; une langue n'entre que complète.
- **Thème sombre WPF** : `MueTheme` (`System`, `Light`, `Dark`), `SetTheme`, barre de titre sombre, contraste élevé respecté. Rien ne change tant que l'appli n'appelle pas Mue.
- **Une liste de clés de style** commune à WPF et Avalonia ; contraste WCAG AA vérifié par les tests.
- **Aide en confirmation** quand l'installateur peut arrêter un programme apparu entre-temps.
- **Téléchargement et installation séparés**, sur option (`UpdatesDialogOptions.SeparateInstallStep`).
- **Appli d'exemple** `samples/Mue.Sample.Wpf`, avec captures neutres.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/02-blockers-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/02-blockers-light.gif" alt="Démonstration : la fenêtre liste les programmes encore ouverts, l'utilisateur laisse les sessions cochées d'avance à l'installateur, ferme un programme et installe" width="960">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/04-confirmation-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/04-confirmation-light.gif" alt="Démonstration : l'hôte annonce ce que fait l'installation, Échap la reporte, puis l'utilisateur confirme et l'appli redémarre sur la nouvelle version" width="960">
</picture>

## Paquets

| Paquet | Cibles | Contenu |
| --- | --- | --- |
| `Mue` | net8.0, net10.0 | Moteur, états, contrats, détection des bloqueurs (Windows et macOS) |
| `Mue.Velopack` | net8.0, net10.0 | Backend Velopack (1.2.161 exactement) |
| `Mue.Wpf` | net8.0-windows, net10.0-windows | Fenêtres WPF et `UpdateBadge` |
| `Mue.Avalonia` | net8.0, net10.0 | Fenêtres et `UpdateBadge` Avalonia 12.1.4 |

Le cœur ne référence ni WPF ni Velopack. Le détail de la détection des bloqueurs est plus bas, dans [Détection des bloqueurs](#détection-des-bloqueurs).

## Démarrage rapide

```sh
dotnet add package Mue.Velopack
dotnet add package Mue.Wpf
```

Tant que Mue n'est pas publié sur nuget.org, ajoutez d'abord le flux local : voir [Publication locale](#publication-locale).

```csharp
using Mue;
using Mue.Velopack;

UpdateTexts.AppName = "Mon application";

var backend = new VelopackBackend(
    new VelopackBackendOptions("https://github.com/vous/mon-app") { FallbackVersion = "0.0.0" });

var service = new UpdateService(backend, new UpdateServiceOptions
{
    PendingStore = new FileUpdatePendingStore(pendingPath),
});

service.StartPolling();                        // recherche planifiée
var launch = service.VerifyLaunch();           // None, Succeeded ou RolledBack

var dialog = new Mue.Wpf.UpdatesDialog(service, host);
dialog.ShowDialog();
```

`host` est votre `IUpdateDialogHost` : il prépare l'application avant l'installation, la quitte, affiche les erreurs, construit les liens de release, et fournit le chercheur de bloqueurs (`WindowsUpdateBlockerFinder` ou `MacUpdateBlockerFinder`) et le classifieur (`ProgramClassifier` par défaut). Appelez `service.Dispose()` à l'arrêt. Avec Avalonia : `new Mue.Avalonia.UpdatesDialog(service, host)`.

La pastille est un contrôle simple (`xmlns:mue="clr-namespace:Mue.Wpf;assembly=Mue.Wpf"`) :

```xml
<mue:UpdateBadge VersionIndicator="{Binding Updates.VersionIndicator}"
                 Status="{Binding Updates.Status}" HasRelease="{Binding Updates.HasRelease}"
                 Command="{Binding OpenUpdates}" />
```

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/03-badge-dark.gif">
  <img src="https://raw.githubusercontent.com/syl-craft/Mue-media/main/videos/03-badge-light.gif" alt="Démonstration : une pastille « v1.2.0 → 1.3.0 » apparaît dans l'application et ouvre la fenêtre de mise à jour" width="960">
</picture>

## Personnalisation

**Textes.** Le français et l'anglais sont intégrés. Implémentez `IUpdateTexts` et passez-le à `UpdateTexts.Configure(...)`. `TryGet` indique les clés que vous fournissez ; les autres retombent sur les ressources de Mue selon `CurrentUICulture`. `Configure(null)` rétablit les textes intégrés.

**Styles.** Les fenêtres lisent les ressources de votre application : les styles `PrimaryButton`, `SecondaryButton`, `DangerButton` et `DialogFooter`, plus un ensemble de brosses `Brush.*`. `MueStyleKeys.Required` les énumère et `MueStyleKeys.Missing(...)` vérifie celles qui manquent. Une application sans thème appelle `MueStyles.AddMissingDefaults(app)` : seules les clés absentes sont ajoutées, votre palette n'est jamais écrasée. Détail : [Ressources WPF requises](#ressources-wpf-requises) et [Intégration Avalonia](#intégration-avalonia).

**Comportement.** `UpdateServiceOptions` accepte un journal, une planification, un prédicat `CanSearch`, un fournisseur de temps et un stockage du marqueur. `VelopackBackendOptions` accepte `Prerelease`, `RestartArgs`, `FallbackVersion`, `UserAgent`, `ShutdownBehavior` et `LatestReleaseOnly`.

## Plateformes

- Windows : moteur, fenêtres WPF et Avalonia, détection des bloqueurs.
- macOS 15 ou plus récent : moteur, fenêtres Avalonia, détection des bloqueurs (`AskToQuit` par défaut).
- La mise à jour ne s'applique qu'à une application installée avec Velopack. Sinon, la fenêtre renvoie vers le téléchargement manuel.

## Intégration par un hôte

Les textes anglais et français sont intégrés. Configurer un fournisseur est facultatif :

```csharp
UpdateTexts.AppName = "Mon application";
UpdateTexts.Configure(new HostUpdateTexts());
var service = new UpdateService(backend, new UpdateServiceOptions
{
    Log = log,
    Schedule = schedule,
    CanSearch = () => true,
    Time = TimeProvider.System,
    PendingStore = new FileUpdatePendingStore(profilePendingPath),
});
var dialog = new Mue.Wpf.UpdatesDialog(service, dialogHost);
dialog.ShowDialog();
```

Les noms HostUpdateTexts, backend, log, schedule et dialogHost représentent les implémentations
de l'hôte. Celui-ci fournit :

- **IUpdateTexts** : `TryGet` indique les clés connues ; `T`, `F`, `Formatting` et `LanguageChanged` gardent leur rôle.
  Les clés inconnues retombent sur les ressources de Mue selon `CurrentUICulture`. `Configure(null)` rétablit les textes intégrés.
  Les ressources françaises voyagent dans les assemblies satellites des paquets ; SmartFormat porte les pluriels.
  `UpdateTexts.AppName` vaut par défaut le nom de l'assembly d'entrée. Le jeton littéral
  `{AppName}` des ressources de Mue est remplacé avant `T` et le formatage `F` ; les textes
  fournis par l'hôte restent intacts.
- **IUpdateLog** : journalisation des erreurs. SilentUpdateLog convient à un hôte sans journal.
- **IUpdateSchedule** : préférence Automatic, dernière recherche et persistance de RecordCheck.
  MemoryUpdateSchedule convient aux fixtures.
- **IUpdateBackend** : état de l'installation, version, recherche, téléchargement et application.
  Mue.Velopack fournit VelopackBackend ; FakeUpdateBackend permet les essais sans réseau.
- **IUpdateBlockerClassifier** : identification des relais et exclusions propres à l'application.
  ProgramClassifier traite les processus comme des programmes ordinaires.
- **IUpdateDialogHost** : vérification sans effet de bord (`CheckAsync`), préparation (`PrepareAsync`), transfert avant application, reprise en cas d'échec,
  sortie, erreurs, liens de release, journal, classificateur et IUpdateBlockerFinder.
  Le propriétaire est un object ; le contrat ne dépend pas de WPF.

L'hôte résident appelle StartPolling puis Dispose à l'arrêt. La recherche commence après
20 secondes, réévalue toutes les 15 minutes la permission et la préférence, et respecte six heures
entre recherches. Aucun téléchargement ne déclenche à lui seul l'application de la mise à jour.

`ShutdownBehavior` décrit le comportement de l'installeur : `Refuse` refuse les programmes
encore ouverts, `AskToQuit` leur demande de quitter, `Kill` les arrête. Le backend Velopack
choisit `AskToQuit` sur macOS, `Kill` ailleurs. DockPad choisit `Refuse` pour ses binaires
adaptés à **Velopack 1.2.161** : ne pas monter cette dépendance implicitement.

`CheckAsync(owner, token)` retourne `UpdatePreparationCheck(CanContinue, Summary)` sans fermer
ni libérer de session. Après téléchargement : vérification, recherche des bloqueurs, dialogue
si nécessaire, contrôle final sans enrichissement, puis une seule préparation, `BeforeApply`,
`Apply`, `ExitAfterApply`. Reporter ou fermer le dialogue des bloqueurs ne prépare pas l'hôte.
Le résumé facultatif apparaît dans les deux fenêtres ; nul, son emplacement reste replié.

### Accord et programmes ignorés

1. `CheckAsync` annonce les effets de la préparation dans `Summary`, sans les exécuter.
2. Sans résumé et sans bloqueur, le parcours continue directement. Sans résumé, une liste
   vidée par **Réessayer** ou par une fermeture vaut accord, comme auparavant.
3. Avec un résumé non nul (même vide), le dialogue s'ouvre toujours. Sans bloqueur, il affiche
   **Confirmer l'installation**, le résumé et **Confirmer et installer**. Seul ce bouton vaut accord.
4. Avec des bloqueurs, le résumé reste visible. **Fermer N programmes et installer** autorise
   leur fermeture. Si **Réessayer** ou cette fermeture vide la liste, le dialogue reste ouvert
   en confirmation ; l'utilisateur confirme avant toute préparation.
5. Pour `Kill` ou `AskToQuit`, décocher un groupe accepte de le laisser à l'installeur.
   Le bouton ferme les groupes cochés, puis poursuit si seuls les ignorés restent.
   Sans groupe coché, **Installer sans les fermer** donne cet accord directement.
   Un processus `Unavailable` peut lui aussi être ignoré ; il ne peut pas être fermé.
6. Les programmes cochés qui restent ouverts passent dans la liste des restants, avec
   **Forcer l'arrêt et installer**. `Kill` annonce un arrêt forcé par l'installeur ; `AskToQuit`
   annonce une demande de quitter, avec un risque d'interruption sans enregistrement.
   `Refuse` exige toujours la disparition de tous les bloqueurs et ne permet pas d'ignorer.
7. Le contrôle final (`enrich: false`) accepte seulement les identités ignorées lors de l'accord
   (PID et heure de démarrage). Un nouveau processus, même avec un PID réutilisé, bloque.
   Ensuite seulement : `PrepareAsync`, `BeforeApply`, `Apply`, `ExitAfterApply`.

**Options (0.4.0).** `new UpdatesDialog(service, host, new UpdatesDialogOptions { SeparateInstallStep = true })`
sépare le parcours en deux : **Télécharger** télécharge seulement et s'arrête sur *Nouvelle version prête* ;
**Redémarrer et installer** lance ensuite la préparation et l'installation. Sans options, un seul clic télécharge
et installe, comme en 0.3.0. `UpdatesDialogOptions.ShowBlockers` remplace l'affichage des bloqueurs (tests). Les
constructeurs 0.3.0 restent ; seul l'appel littéral `new UpdatesDialog(service, host, null)` devient ambigu à la
compilation.

**Aide en confirmation.** Avec `Kill` ou `AskToQuit`, la fenêtre de confirmation prévient que les programmes qui
utiliseraient les fichiers de l'appli d'ici là seront arrêtés ou recevront une demande de quitter. Avec `Refuse`,
elle n'affiche que le résumé.

**Un groupe par programme.** Plusieurs instances d'un même programme forment un seul groupe : on les ferme ou on
les ignore ensemble.

**Reporter**, Échap ou la fermeture de la fenêtre ne donnent aucun accord et n'appellent jamais
`PrepareAsync`. `UpdateBlockersDialog` reçoit désormais un `UpdateBlockerScan`, et son délégué
`refresh` retourne ce même type. `scan.Incomplete` affiche un avertissement séparé de l'état de
fermeture incomplète (`incomplete`, utilisé par les fixtures). `IgnoredBlockers` expose les identités
acceptées pour le contrôle final. `UpdatePresentation.CloseResultText` traduit les cinq résultats
de `CloseAsync`.

`Apply` écrit la version attendue avant de lancer le backend et la retire si celui-ci lève.
Au démarrage résident, appeler `service.VerifyLaunch()` une fois : `None`, `Succeeded` ou
`RolledBack`, avec versions attendue et courante. Le marqueur est consommé. Seule l'égalité
textuelle, sans préfixe `v`, est examinée. Le stockage par défaut est en mémoire ; utiliser
`FileUpdatePendingStore` dans le profil pour traverser les redémarrages.

`BlockerQuery` contient les chemins à examiner (dossiers ou fichiers), et exclut le processus
courant sauf `IncludeCurrentProcess`. `Find(query, classifier, enrich)` retourne un
`UpdateBlockerScan` ; une détection en échec lève. `Incomplete` signale au moins un processus
`Unavailable`, gardé lorsqu'il est signalé par Restart Manager mais reste illisible. Son
arrêt retourne `AccessDenied`. `CloseAsync` retourne `Closed`, `StillRunning`, `AccessDenied`,
`Gone` ou `IdentityChanged`, avec contrôle PID/date à chaque tentative.

Les recherches reçoivent un jeton annulé par `Dispose`. Une recherche automatique est aussi
annulée à la désactivation, ou au cycle suivant si `CanSearch` devient faux. L'annulation
n'affiche pas d'erreur. `VelopackBackendOptions.UserAgent` vaut `Mue` par défaut ;
`LatestReleaseOnly = true` limite GitHub à `/releases/latest`, sans repli vers une autre release.
Cette option vaut `false` par défaut, notamment pour DockPad.

UpdatesDialog n'a pas de constructeur sans paramètre : la fabrique appartient à l'hôte.
L'extension XAML `{mue:T Clé}` crée une liaison OneWay vers un indexeur singleton, actualisé sur
LanguageChanged. L'hôte reste responsable du thread d'interface pour les changements de langue.

## Ligne de commande

Pour les scripts et les agents IA : installer ou mettre à jour sans aucune fenêtre. La mise à jour par la fenêtre
reste au clic.

**Première installation.** L'installateur Velopack de l'appli installe en silence (vérifié sur Velopack 1.2.161) :

```powershell
MonAppli-win-Setup.exe --silent                   # -s : aucune fenêtre, répond oui à toutes les questions
MonAppli-win-Setup.exe --silent --installto D:\Applis\MonAppli --log setup.log
```

**Mise à jour.** L'appli relaie un verbe de sa propre ligne de commande à `UpdateCommand`, avec le même
`IUpdateDialogHost` que la fenêtre :

```csharp
if (args.FirstOrDefault() == "update")
    return await UpdateCommand.RunAsync(service, host, args.Skip(1).ToArray(), Console.Out);
```

Même déroulé que la fenêtre (recherche, téléchargement, vérification de l'hôte, résumé, bloqueurs, contrôle final, une
seule préparation, installation), sans fenêtre. La commande ne compte jamais son propre processus parmi les bloqueurs.

| Option | Effet |
|---|---|
| `--check` | Recherche seulement |
| `--yes` | Accepte le résumé annoncé par l'hôte (`UpdatePreparationCheck.Summary`) |
| `--close-blockers` | Ferme les programmes qui utilisent les fichiers de l'appli (fermeture douce), relais MCP et **fenêtres ouvertes de l'appli** compris |
| `--restart` | Relance l'appli après l'installation (par défaut, pas de relancement : le script décide) |
| `--json` | Écrit un seul objet JSON |

| Code | `status` | Cas |
|---|---|---|
| 0 | `up-to-date` / `updated` | Déjà à jour, ou installation lancée |
| 10 | `available` | `--check` : une nouvelle version existe |
| 20 | `blocked` | Des programmes utilisent les fichiers de l'appli (sans `--close-blockers`, ou encore ouverts après fermeture) |
| 21 | `consent-required` | L'hôte annonce un résumé, sans `--yes` |
| 22 | `unavailable` | Appli non installée par Velopack |
| 23 | `refused` | L'hôte refuse (`CanContinue` ou `PrepareAsync` faux) |
| 1 | `error` | Échec de recherche, de téléchargement ou d'installation |

JSON : `status`, `current`, `available` (version visée), `summary`, `blockers` (`name`, `pid`, `detail`, `relay`,
`group`) et `message`. `relay` indique à un agent que la fermeture coupera des sessions MCP, peut-être la sienne.

- **Le code 0 `updated` arrive avant la fin de la copie** : Velopack remplace les fichiers une fois l'appli quittée.
  Pour savoir quand c'est fini, relancer `update --check --json` jusqu'à obtenir `status` `up-to-date` avec `current`
  égal à la version `available` obtenue.
- **Une appli fenêtrée (WinExe) n'est pas attendue** par un appel simple depuis PowerShell ou cmd. Vérifié sur l'appli
  d'exemple :

```powershell
$result = & .\MonAppli.exe update --json | Out-String   # attend, capture le JSON ; code dans $LASTEXITCODE
$p = Start-Process .\MonAppli.exe -ArgumentList "update --json" -Wait -PassThru -NoNewWindow -RedirectStandardOutput out.json
$p.ExitCode
```

  Avec cmd, `start /wait "" MonAppli.exe update --json` donne le code dans `%ERRORLEVEL%` mais pas la sortie : lire le
  JSON depuis PowerShell. Une WinExe n'écrit dans la console qu'après `AttachConsole(ATTACH_PARENT_PROCESS)`, ou dans
  une sortie redirigée.
- **Une appli de la zone de notification** peut répondre `StillRunning` à une fermeture douce si sa fenêtre se cache au
  lieu de quitter. L'hôte gère ce cas : relayer `update` à l'instance ouverte, ou la faire quitter sur une fermeture douce.
- **macOS :** le mode silencieux n'est pas encore vérifié sur une vraie machine.

## Textes et langues

Mue fournit l'anglais et le français. **Une langue n'entre que complète** : un test vérifie que chaque langue
intégrée a exactement les clés des ressources neutres.

`UpdateTextKeys.All` énumère toutes les clés de texte. `UpdateTextKeys.Missing(texts)` retourne celles que votre
`IUpdateTexts` ne fournit pas. Il sert aux applis qui fournissent **tous** leurs textes (langue propre,
pseudo-langue) : ajoutez `Assert.Empty(UpdateTextKeys.Missing(mesTextes))` à vos tests, et une clé ajoutée par
une nouvelle version de Mue y apparaît au lieu d'une fenêtre aux langues mélangées. Pour une appli qui ne
surcharge qu'une partie des textes, ou aucun, la liste est non vide par conception : elle est informative.

**Langue choisie par l'appli.** Les textes intégrés de Mue suivent `CurrentUICulture`. Une appli qui choisit sa
langue sans suivre Windows règle cette culture et prévient Mue par `LanguageChanged` :

```csharp
var texts = new AppLanguageTexts();
UpdateTexts.Configure(texts);
texts.Switch(CultureInfo.GetCultureInfo("fr"));

/// <summary>Mue garde ses textes anglais et français, mais suit la langue choisie dans l'appli.</summary>
sealed class AppLanguageTexts : IUpdateTexts
{
    public CultureInfo Formatting { get; private set; } = CultureInfo.CurrentUICulture;
    public event EventHandler? LanguageChanged;

    // Aucune clé fournie : Mue répond avec ses propres textes, dans la langue de CurrentUICulture.
    public bool TryGet(string key, out string text) { text = ""; return false; }
    public string T(string key) => key;
    public string F(string key, params object?[] args) => key;

    public void Switch(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        Formatting = culture;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

## Thèmes

`Mue.UpdateStyleKeys.Required` est la liste unique des ressources lues par les fenêtres, commune à WPF et Avalonia
(`Mue.Wpf.MueStyleKeys` et `Mue.Avalonia.MueStyleKeys` y renvoient). Les jeux par défaut ont un accent bleu
neutre et respectent le contraste WCAG AA, en clair et en sombre.

**WPF.** Choisir le thème des valeurs par défaut de Mue et de la barre de titre de ses fenêtres :

```csharp
MueStyles.AddMissingDefaults(app, MueTheme.System); // ajoute les clés absentes ; System suit le mode clair/sombre de Windows
MueStyles.SetTheme(app, MueTheme.Dark);             // à chaque bascule de thème de l'appli
MueStyles.SetTheme(app, null);                      // rendre toute la main à l'appli
```

- **Tant que l'appli n'appelle ni `AddMissingDefaults` ni `SetTheme`, Mue.Wpf ne touche ni aux ressources, ni à la
  barre de titre, ni aux événements Windows.** Une appli qui fournit toute sa palette et peint ses barres de titre
  garde son rendu.
- Mue ne remplace que les valeurs qu'il a lui-même ajoutées : une clé fournie par l'appli n'est jamais touchée.
- `System` s'abonne aux changements de réglages Windows et se réapplique ; `SetTheme(app, null)` se désabonne et
  retire les valeurs ajoutées par Mue.
- Contraste élevé Windows : avec `System`, Mue complète les clés absentes avec les couleurs système, jamais avec
  son jeu clair ou sombre, et laisse la barre de titre à Windows.
- Barre de titre : sombre (mode sombre immersif DWM) quand le thème effectif est sombre. `MueStyles.ManageTitleBar
  = false` la laisse à l'appli ; `MueStyles.WindowOpened` lui donne chaque fenêtre de Mue avant son affichage.
- `AddMissingDefaults(app)` sans thème vaut désormais `System` (il ne fournissait que le clair en 0.3.0).

**Avalonia.** Les fenêtres suivent le `ThemeVariant` de l'application. Sous Windows, Avalonia peint la barre de
titre d'après `ActualThemeVariant`. **Limite connue, macOS :** la barre de titre suit l'apparence du système, pas
le `ThemeVariant` de l'appli.

## Ressources WPF requises

Mue.Wpf reprend les ressources de l'application, sans embarquer de thème. Fournir ces styles :

- PrimaryButton, SecondaryButton et DangerButton : styles Button.
- DialogFooter : style Border.

Fournir les brosses suivantes, avec des valeurs adaptées au thème actif :

```text
Brush.Surface             Brush.SurfaceCard        Brush.SurfaceMuted
Brush.Text                Brush.TextLabel          Brush.TextDetail
Brush.TextSmall           Brush.TextSubtle         Brush.Separator
Brush.Border              Brush.Accent             Brush.AccentText
Brush.AccentTint          Brush.AccentSelected     Brush.AccentBorderSoft
Brush.Danger              Brush.DangerSurface      Brush.DangerBorder
Brush.WarnTextAlt         Brush.TextDisabled       Brush.AccentTintStrong
```

Les références dynamiques suivent les remplacements de ressources de l'hôte. Ses styles
implicites de contrôles (case à cocher, barre de progression, etc.) s'appliquent aussi.
BoolToVisibilityConverter est déclaré dans UpdatesDialog : l'hôte n'a pas à fournir cette clé.

`MueStyleKeys.Required` énumère le contrat exact et `MueStyleKeys.Missing(app.TryFindResource)`
permet de le tester. Un hôte sans thème peut appeler `MueStyles.AddMissingDefaults(app)` :
seules les clés absentes sont ajoutées aux ressources de l'application. Aucun dictionnaire
n'est fusionné dans les fenêtres. DockPad conserve entièrement ses palettes.

`UpdateBadge` est un bouton autonome, sans barre imposée : `VersionIndicator`, `Status`,
`HasRelease`, `Command` et événement `Click`. Sa cible transparente mesure 40 px de haut et sa
pastille 26 px ; les brosses dynamiques suivent le thème de l'hôte.

```xml
<mue:UpdateBadge VersionIndicator="{Binding Updates.VersionIndicator}"
                 Status="{Binding Updates.Status}" HasRelease="{Binding Updates.HasRelease}"
                 Command="{Binding OpenUpdates}" />
```

## Intégration Avalonia

Après les ressources et le thème de l'hôte, appeler explicitement
`Mue.Avalonia.MueStyles.AddMissingDefaults(app)` au démarrage. Les clés sont les mêmes que
`Mue.Wpf.MueStyleKeys.Required` ; les trois boutons et `DialogFooter` sont des `ControlTheme`
Avalonia ciblant respectivement `Button` et `Border`. Le repli clair/sombre ne fournit que les
clés absentes, dans le premier dictionnaire fusionné, derrière les ressources de l'hôte.
Les fenêtres ne fusionnent aucun dictionnaire et utilisent `DynamicResource`.

```csharp
var dialog = new Mue.Avalonia.UpdatesDialog(service, dialogHost);
await dialog.ShowDialog(owner);
```

`dialogHost` implémente le contrat générique `IUpdateDialogHost` ; les propriétaires reçus
sont des `Avalonia.Controls.Window`. Le thème de contrôles de l'hôte (Fluent ou autre) reste
responsable des cases à cocher et barres de progression. L'extension `{mue:T Clé}` suit
`UpdateTexts.LanguageChanged` sur le thread d'interface. `UpdateBadge` expose les mêmes
propriétés, `Command`, `CommandParameter` et `Click` que le contrôle WPF ; la cible est
entièrement cliquable sur 40 px et la pastille mesure 26 px.

Les tests `Mue.Avalonia.Tests` utilisent Skia et `Avalonia.Headless.XUnit` sur les deux cibles.
Ils écrivent les captures dans `artifacts/avalonia/net8.0` et `artifacts/avalonia/net10.0`,
dossier ignoré par git.

## Détection des bloqueurs

Le cœur ne référence ni WPF ni Velopack et ne cible pas Windows. Seul
WindowsUpdateBlockerFinder est réservé à Windows, avec System.Management 8.0.0 et les appels
natifs. UpdateBlockers conserve les opérations pures de sélection et d'identité ; la recherche
et la fermeture passent par IUpdateBlockerFinder.

Sur macOS 15+, `MacUpdateBlockerFinder` reçoit une `UpdateBlockerQuery` contenant des chemins
absolus de bundles `.app`, de dossiers ou d'exécutables. Les chemins sont résolus par `realpath`
avant le filtrage, avec une comparaison sensible à la casse. `IncludeCurrentProcess` permet
de lister le processus courant ; sa fermeture reste refusée.
La fermeture douce attend cinq secondes ; le constructeur
`new MacUpdateBlockerFinder(exitTimeout: TimeSpan.FromSeconds(7), log: log)` permet de régler ce délai.
Conserver les objets retournés par `Find` et utiliser la même instance pour `CloseAsync` :
elle garde leur audit token natif. Sans ce token, aucun signal n'est envoyé.
`Find(query, classifier, enrich)` retourne un `UpdateBlockerScan`. Un processus dont le chemin
est illisible est ignoré : rien ne le rattache aux chemins demandés (comme sous Windows, hors signalement
Restart Manager). Un exécutable retenu dont l'identité est illisible est listé avec `Unavailable`.
Ces bloqueurs ne sont pas présélectionnés, rendent le scan `Incomplete` et ne peuvent pas être fermés.
`CloseAsync(blocker, force, token)` retourne les cinq valeurs de `UpdateBlockerCloseResult` ;
l'annulation interrompt l'attente sans envoyer de signal supplémentaire.

## Appli d'exemple

`samples/Mue.Sample.Wpf` est une appli « Notebook » dont la mise à jour 1.3.0 est simulée par `FakeUpdateBackend`,
sans réseau. `Mue.Sample.Wpf.exe --capture artifacts/wpf` écrit des captures neutres des fenêtres de Mue (clair et
sombre, anglais et français) dans `artifacts/`, ignoré par git. `Mue.Sample.Wpf.exe update --json` lance la ligne de
commande sur la mise à jour simulée. L'exemple n'est pas empaqueté.

## Publication locale

Installer les SDK .NET 8 et .NET 10 et le support WPF sur Windows, puis exécuter :

```powershell
./pack.ps1
./pack.ps1 -Feed C:\mon-flux-nuget
./pack.ps1 -Feed C:\mon-flux-nuget -Version 0.3.0-b3.1 -SkipTests
```

Le script compile et teste la solution, puis empaquette les quatre projets en Release avec -m:4.
Le flux par défaut est C:\Users\Sylvain\nuget-local. Pour un projet consommateur, définir
RestoreAdditionalProjectSources à partir d'une propriété MueFeed surchargeable :

```xml
<PropertyGroup>
  <MueFeed Condition="'$(MueFeed)' == ''">C:\Users\Sylvain\nuget-local</MueFeed>
  <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MueFeed)</RestoreAdditionalProjectSources>
</PropertyGroup>
```

Passer -p:MueFeed=... aux commandes dotnet pour changer de flux. Ne pas placer de chemin
local absolu dans un nuget.config versionné.

**Une version publiée ne se republie pas.** NuGet conserve les paquets dans ~/.nuget/packages
(ou NUGET_PACKAGES) : remplacer un .nupkg ne met pas à jour les consommateurs déjà restaurés.
Augmenter Version dans Directory.Build.props et mettre à jour le changelog avant une nouvelle
publication. Le script refuse d'écraser une version présente dans le flux.

## Tests

`dotnet build Mue.sln -m:4` puis `dotnet test Mue.sln -m:4 --filter "Category!=RealMachine"`.
Les tests Windows `RealMachine` consultent les vrais processus et Restart Manager ; les exécuter
sur une machine Windows hors bac à sable avec `--filter "Category=RealMachine"`. Ils ne sont pas
désactivés. Les tests natifs Mac portent `RealMachine` et `MacNative` ; ils sont ignorés hors
macOS 15+. Sur Mac, les lancer avec `dotnet test tests/Mue.Tests/Mue.Tests.csproj -m:4 --filter "Category=MacNative"`.
Les tests HTTP utilisent des réponses simulées, sans accès réseau.

## Licence

MIT, voir [LICENSE](LICENSE).
