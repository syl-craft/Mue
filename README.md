# Mue

Bibliothèque de mise à jour extraite de DockPad : moteur, backend Velopack et dialogues WPF,
indépendants des choix de l'application hôte. Rien ne s'installe automatiquement.

## Paquets

| Paquet | Cibles | Contenu |
| --- | --- | --- |
| Mue | net8.0, net10.0 | États, recherche, téléchargement, notes, contrats et blocages |
| Mue.Velopack | net8.0, net10.0 | Backend et chemins Velopack ; dépendance exacte **1.2.161** |
| Mue.Wpf | net8.0-windows, net10.0-windows | UpdatesDialog, UpdateBlockersDialog et traduction XAML |
| Mue.Avalonia | net8.0, net10.0 | Port Avalonia 12.1.4 des dialogues et UpdateBadge (T2 en cours de validation) |

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

## Publication locale

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
Ils écrivent les captures dans `artifacts/avalonia/net8.0` et `artifacts/avalonia/net10.0`.
Les limites du contrat 0.2.0 et la matrice de validation sont décrites dans
[docs/t2-avalonia.md](docs/t2-avalonia.md). Les libellés manquants empêchent encore de valider
la totalité de T2 ; aucun ajout au contrat n'a été effectué.

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

Licence MIT, syl-craft.
