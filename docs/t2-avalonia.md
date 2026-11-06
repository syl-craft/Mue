# T2 — Port Avalonia

Base : `sylvain/b2-contrat-0-2-0`, commit `9fbd25e`. Travail isolé sur
`sylvain/t2-mue-avalonia`. Ni le contrat, ni Mue.Wpf, ni Mue.Velopack, ni la version 0.2.0
ne sont modifiés. Aucun paquet, tag ou push n'est produit.

## État de validation

Le port compile et ses tests passent. Les points 2, 4, 6 et 7 de la revue NIN-348-T2
sont corrigés dans Avalonia. Les points 1 (accord sans bloqueur), 3 (choix Ignorer)
et 5 (contenu D9) restent confiés à la session DockPad pour b2 ; cette correction ne
valide donc pas T2 dans son ensemble. Aucun texte supplémentaire n'est introduit dans le cœur.

### Textes provisoires à remonter dans le cœur

`AvaloniaUpdateTexts.T` consulte d'abord `UpdateTexts.T` : les traductions de l'hôte,
puis les ressources existantes du cœur, restent prioritaires. Uniquement si le résultat
est le marqueur de clé absente `[clé]`, Avalonia fournit les replis ci-dessous.
Ils suivent `CurrentUICulture` (français pour `fr`, anglais sinon), comme les ressources
intégrées du cœur, et sont réévalués sur `UpdateTexts.LanguageChanged`.
Les clés de présentation existantes, dont `Update_Later`, sont conservées.

| Clé à remonter | Repli français | Repli anglais |
| --- | --- | --- |
| `Common_Close` | Fermer | Close |
| `Update_DetectionIncomplete` | Détection incomplète : certains programmes n’ont pas pu être identifiés. | Detection incomplete: some programs could not be identified. |
| `Update_CloseResultClosed` | Programme fermé | Program closed |
| `Update_CloseResultStillRunning` | Programme toujours ouvert | Program still running |
| `Update_CloseResultAccessDenied` | Fermeture refusée : accès non autorisé | Cannot close: access denied |
| `Update_CloseResultGone` | Programme déjà terminé | Program already exited |
| `Update_CloseResultIdentityChanged` | Processus remplacé : aucune fermeture effectuée | Process replaced: no closure attempted |

Cette liste et ces traductions sont à intégrer aux ressources fr/en du cœur sur b2,
puis le repli local pourra être supprimé. `Common_Close` est déjà référencé par
`UpdatePresentation`, mais absent des ressources du cœur à cette révision.
`Update_IncompleteState` concerne la fermeture, et ne convient pas à la détection.
Les textes D9 et les mentions DockPad des ressources existantes restent inchangés ;
l'hôte peut les traduire via `IUpdateTexts`.

### Priorité des styles

Les seules clés manquantes sont regroupées dans des `Styles` insérés à l'index 0 de
`Application.Styles`, consultés après les autres styles et les ressources de l'application.
Un thème dans `Application.Styles` (comme `Theme.axaml` dans AlterVNC) gagne donc aussi
s'il est ajouté après le repli. Les tests couvrent les deux ordres, clair et sombre,
l'idempotence et la priorité des dictionnaires de `Application.Resources`.

## Confirmation

`UpdatesDialog` reprend l'ordre du WPF : clic installer, téléchargement, `CheckAsync`,
affichage du résumé, recherche enrichie, dialogue des bloqueurs si nécessaire, recherche
finale sans enrichissement, une seule préparation, `BeforeApply`, `Apply`, `ExitAfterApply`.
Le report et la fermeture de la fenêtre des bloqueurs laissent la version téléchargée prête.
Un téléchargement terminé ou la construction d'une fenêtre n'installe rien.

La première action sur les bloqueurs appelle leur fermeture coopérative. Les identités
encore présentes passent dans la carte des restants avec l'avertissement de perte de données ;
seul un second clic utilise `force: true`. L'identité est laissée au fournisseur du contrat.
Une nouvelle identité ayant réutilisé un PID n'hérite pas du choix précédent.
L’avertissement de perte de données occupe une ligne fixe hors du `ScrollViewer`,
au-dessus des boutons. Ses coordonnées sont testées en français et en anglais, aux deux
tailles, avec vingt processus et des chemins longs, en haut et en bas du défilement.
La détection incomplète est indépendante de la fermeture incomplète et ne préarme jamais
l'action forcée. Les résultats de fermeture sont conservés dans la fenêtre, par processus.
Les relais `IsRelay` passent directement dans les restants après le premier clic : le finder
Windows les arrête de force même avec `force: false`. Avalonia attend donc le second clic
avant de les transmettre au finder (`RelayIsNeverPassedToFinderBeforeForceConfirmation`).

## Assertions

| Exigence | Test principal |
| --- | --- |
| Résumé visible dans les deux fenêtres, sans préparation | `SummaryIsVisibleInBothWindowsBeforeAnyPreparationAndPostponeHasNoEffect` |
| Accord final avant préparation unique et contrôle final | `FinalAgreementPrecedesSinglePreparationAndFinalScan` |
| Reporter et fermer sans préparation | test du résumé et `ClosingBlockersDoesNotPrepare` |
| Arrêt forcé uniquement après seconde confirmation | `ForceRequiresASecondClickAfterCooperativeFailure` |
| Erreur de détection distincte d'une liste vide | `DetectionFailureIsNotAnEmptySuccessfulScan` |
| Résultats, détection et Fermer traduits, fournisseur prioritaire, changement de langue à chaud | `DisplayedTextsUseLocalFallbackOrHostAndFollowLanguageChanges`, `CloseFallbackAlsoAppearsOnSecondaryAction` |
| Avertissement entièrement visible quand Forcer est actif | `ForceWarningStaysInsideWindowAtBothScrollExtremes` |
| Refus, annulation, nouveau bloqueur, échec d'application | `RefusalCancellationAndNewBlockerPreventApplication`, `ApplyFailureResumesHostAndKeepsDownloadedRelease` |
| Double clic et PID réutilisé | test de l'accord final et `CloseDoubleClickDoesNotRepeatOperationsAndIdentityChangeDropsOldSelection` |
| D9 selon capacité, processus indisponible visible | trois cas de `ShutdownBehaviorSelectsD9Texts` |
| Dictionnaire complet intact, seule clé absente ajoutée | `CompleteHostDictionaryIsUntouched`, `OnlyMissingKeysReceiveFallbackAndLaterHostDictionaryWins` |
| Thème dans Application.Styles avant/après le repli | `ApplicationStylesThemeWinsBeforeAndAfterFallback` |
| Thème à chaud et remplacement des ressources de l'hôte | `OpenWindowAndBadgeFollowThemeChangesAndHostReplacements` |
| Badge, clic dans les marges, clavier, commande | `BadgeHasFullyClickableFortyPixelTargetAndTwentySixPixelPill` |
| Repli fr/en existant et langue à chaud | `TextsUseMueFrenchAndEnglishFallbackAndFollowHostLanguageChange` |
| Clés de style identiques à WPF | `StyleContractMatchesWpfExactlyAndNoOtherDynamicKeyIsUsed` |

## Captures

`dotnet test -m:4` régénère 216 PNG Skia, sans fenêtre native, réseau ni processus réel
dans les fixtures Avalonia. Les tests existants de Mue, dont `RealMachine`, sont exécutés séparément
par la même commande. Chaque capture vérifie ses dimensions et la présence des boutons dans
la zone cliente. Les notes longues et les chemins de processus sont défilables.

Chemin : `artifacts/avalonia/{net8.0|net10.0}/{clair|sombre}-{440x360|560x480}/`.

| État | Fichier |
| --- | --- |
| Recherche | `Checking.png` |
| À jour | `Idle.png` |
| Disponible | `Available.png` |
| Téléchargement | `Downloading.png` |
| Prêt | `Ready.png` |
| Préparation avec résumé | `Preparing.png` |
| Installation | `Installing.png` |
| Erreur | `Failed.png` |
| Indisponible | `Unavailable.png` |
| Bloqueurs | `Blockers.png`, `Blockers-details.png` |
| Détection incomplète | `DetectionIncomplete.png`, `DetectionIncomplete-details.png` |
| Variantes de capacité | `Blockers-AskToQuit.png`, `Blockers-Refuse.png` |
| Échec coopératif, trois capacités | `CooperativeFailure-{Kill,AskToQuit,Refuse}.png` et leurs vues `-details` |
| Cinq résultats de fermeture | `CloseResults.png`, `CloseResult-{Closed,StillRunning,AccessDenied,Gone,IdentityChanged}.png` |

Les vues `-details` montrent le bas des listes, les chemins longs et les informations
indisponibles. Les vues `CloseResult-*` amènent chaque résultat dans la zone visible.
Les textes affichés utilisent les traductions françaises ; les tests contrôlent aussi
l’anglais et les surcharges de l’hôte. Une inspection visuelle couvre les deux tailles
et les deux thèmes ; il ne s’agit pas d’une comparaison pixel automatisée. À 440 × 360, la préparation réserve la priorité au résumé
et aux actions ; la zone des notes est réduite. À 560 × 480, plusieurs lignes restent visibles.

## Vérifications locales

- `dotnet build Mue.sln -m:4` : succès, zéro avertissement, zéro erreur.
- `dotnet test -m:4` : 86 tests Mue, 37 tests Avalonia en net8.0, 37 en net10.0 ; aucun ignoré.
- Aucun changement de source du cœur, de WPF ou de Velopack par rapport à `9fbd25e`.
- Aucun build ni parcours du dépôt consommateur DockPad, aucune comparaison pixel de DockPad :
  le jalon de publication reste à effectuer dans sa session. Aucune connexion au Mac.

Le projet Avalonia supprime uniquement AVLN3001 : les fenêtres nécessitent les dépendances
du constructeur et ne sont pas instanciables par URI. Les ressources de thème restent
chargeables par URI. La construction et le test utilisent Avalonia 12.1.4 et xUnit v3,
requis par cette version d'Avalonia.Headless.XUnit ; les tests existants gardent xUnit v2.
