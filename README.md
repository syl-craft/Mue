# Mue

Bibliothèque de mise à jour extraite de DockPad : moteur, backend Velopack et dialogues WPF,
indépendants des choix de l'application hôte. Rien ne s'installe automatiquement.

## Paquets

| Paquet | Cibles | Contenu |
| --- | --- | --- |
| Mue | net8.0, net10.0 | États, recherche, téléchargement, notes, contrats et blocages |
| Mue.Velopack | net8.0, net10.0 | Backend et chemins Velopack ; dépendance exacte **1.2.161** |
| Mue.Wpf | net8.0-windows, net10.0-windows | UpdatesDialog, UpdateBlockersDialog et traduction XAML |

Le cœur ne référence ni WPF ni Velopack et ne cible pas Windows. Seul
WindowsUpdateBlockerFinder est réservé à Windows, avec System.Management 8.0.0 et les appels
natifs. UpdateBlockers conserve les opérations pures de sélection et d'identité ; la recherche
et la fermeture passent par IUpdateBlockerFinder.

## Intégration par un hôte

Configurer les textes avant de créer un modèle ou une fenêtre :

```csharp
UpdateTexts.Configure(new HostUpdateTexts());
var service = new UpdateService(backend, new UpdateServiceOptions
{
    Log = log,
    Schedule = schedule,
    CanSearch = () => true,
    Time = TimeProvider.System,
});
var dialog = new Mue.Wpf.UpdatesDialog(service, dialogHost);
dialog.ShowDialog();
```

Les noms HostUpdateTexts, backend, log, schedule et dialogHost représentent les implémentations
de l'hôte. Celui-ci fournit :

- **IUpdateTexts** : traduction T, formatage F, culture Formatting et événement LanguageChanged.
  UpdateTexts n'a aucun fournisseur par défaut : un accès avant Configure lève une exception explicite.
  Fournir les clés Update_* dans les ressources de l'hôte, avec leurs arguments et règles de pluriel.
- **IUpdateLog** : journalisation des erreurs. SilentUpdateLog convient à un hôte sans journal.
- **IUpdateSchedule** : préférence Automatic, dernière recherche et persistance de RecordCheck.
  MemoryUpdateSchedule convient aux fixtures.
- **IUpdateBackend** : état de l'installation, version, recherche, téléchargement et application.
  Mue.Velopack fournit VelopackBackend ; FakeUpdateBackend permet les essais sans réseau.
- **IUpdateBlockerClassifier** : identification des relais et exclusions propres à l'application.
  ProgramClassifier traite les processus comme des programmes ordinaires.
- **IUpdateDialogHost** : préparation, transfert avant application, reprise en cas d'échec,
  sortie, erreurs, liens de release, journal, classificateur et IUpdateBlockerFinder.
  Le propriétaire est un object ; le contrat ne dépend pas de WPF.

L'hôte résident appelle StartPolling puis Dispose à l'arrêt. La recherche commence après
20 secondes, réévalue toutes les 15 minutes la permission et la préférence, et respecte six heures
entre recherches. Aucun téléchargement ne déclenche à lui seul l'application de la mise à jour.

ForcesShutdown vaut true par défaut pour le backend Velopack standard. Ne le mettre à false
que si le moteur natif garantit effectivement l'absence de fermeture forcée. DockPad fournit
ses propres binaires adaptés à **Velopack 1.2.161** : ne pas monter cette dépendance implicitement.

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
Brush.WarnTextAlt
```

Les références dynamiques suivent les remplacements de ressources de l'hôte. Ses styles
implicites de contrôles (case à cocher, barre de progression, etc.) s'appliquent aussi.
BoolToVisibilityConverter est déclaré dans UpdatesDialog : l'hôte n'a pas à fournir cette clé.

## Publication locale

Installer les SDK .NET 8 et .NET 10 et le support WPF sur Windows, puis exécuter :

```powershell
./pack.ps1
./pack.ps1 -Feed C:\mon-flux-nuget
```

Le script compile et teste la solution, puis empaquette les trois projets en Release avec -m:4.
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

Licence MIT, syl-craft.
