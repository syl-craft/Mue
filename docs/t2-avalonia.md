# T2 — Port Avalonia

Base intégrée : `sylvain/b3-accord-ignorer-textes`, commit `6819162`, version 0.3.0.
Travail isolé sur `sylvain/t2-mue-avalonia`. Le merge `594b082` conserve les projets
Avalonia et les tests WPF dans la solution. Les sources du cœur, de Mue.Wpf et de
Mue.Velopack restent identiques à b3. Aucun paquet, tag ou push n'est produit.

## État de validation

Le port reprend le parcours b3 : accord explicite pour tout résumé non nul,
confirmation après une liste vidée, choix des groupes ignorés pour `Kill` et
`AskToQuit`, contrôle final par identité et textes fournis par le cœur.
Les tests headless couvrent ces comportements en .NET 8 et .NET 10.

### Textes du cœur

Les replis locaux `AvaloniaUpdateTexts` et `BlockerTexts` sont supprimés.
Les textes viennent de `UpdateTexts`, avec priorité aux traductions de l'hôte et
réévaluation lors de `LanguageChanged`. Le cœur remplace `{AppName}` dans ses
ressources ; Avalonia n'effectue aucune substitution supplémentaire.

Les cinq résultats de fermeture passent par `UpdatePresentation.CloseResultText`.
`Update_ScanIncomplete` apparaît lorsque le `UpdateBlockerScan` est incomplet.
Les titres et actions utilisent `Update_ConfirmTitle`, `Update_ConfirmInstall`,
`Update_BlockedTitle` et `Update_InstallIgnoring`. La clé de présentation
`Common_Close` est dirigée côté Avalonia vers `Update_CloseWindow`, fournie par le cœur.
Les autres clés de présentation, dont `Update_Later`, sont conservées.

### Priorité des styles

Les seules clés manquantes sont regroupées dans des `Styles` insérés à l'index 0 de
`Application.Styles`, consultés après les autres styles et les ressources de l'application.
Un thème dans `Application.Styles` (comme `Theme.axaml` dans AlterVNC) gagne donc aussi
s'il est ajouté après le repli. Les tests couvrent les deux ordres, clair et sombre,
l'idempotence et la priorité des dictionnaires de `Application.Resources`.

## Confirmation

`UpdatesDialog` reprend l'ordre du WPF : clic installer, téléchargement, `CheckAsync`,
affichage du résumé, recherche enrichie, dialogue si des bloqueurs ou un résumé sont présents, recherche
finale sans enrichissement, une seule préparation, `BeforeApply`, `Apply`, `ExitAfterApply`.
Reporter, fermer la fenêtre ou appuyer sur Échap laisse la version téléchargée prête,
sans accord, contrôle final ni préparation. Un résumé nul conserve le parcours sans confirmation.
Un résumé non nul, même vide, exige un accord explicite. Sans bloqueur, le dialogue affiche
« Confirmer l'installation ». Si Réessayer ou une fermeture vide la liste, il passe dans cet
état et attend le clic « Confirmer et installer ».

Avec `Kill` ou `AskToQuit`, les groupes décochés sont ignorés lors du clic d'installation.
Sans sélection, « Installer sans les fermer » accepte directement. Après une fermeture,
le dialogue accepte si seuls les processus ignorés restent présents. `IgnoredBlockers`
expose leurs couples PID/date de démarrage. `UpdateBlockers.HasUnacceptedBlockers` tolère
ces identités au contrôle final et refuse tout nouveau processus ou PID réutilisé.
`Refuse` conserve son comportement strict.
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
| Résumé nul, vide ou renseigné, avec ou sans bloqueur | `PreparationFollowsAgreementAndFinalScan` |
| Reporter, fermer ou Échap sans préparation | `PostponeWindowCloseAndEscapeNeverPrepare` |
| Réessayer vide la liste et attend confirmation | `RetryWithEmptyListOnlyAgreesWithoutSummary` |
| Groupe décoché ignoré, y compris illisible | `IgnoredGroupIsAcceptedWithoutClosingIt` |
| Fermeture des cochés, seuls les ignorés peuvent rester | `ClosingSelectedOnlyAcceptsAlreadyIgnoredRemainingProcesses` |
| Nouveau processus ou PID réutilisé refusé au contrôle final | `FinalScanRejectsAnIdentityNotAccepted` |
| Refuse ne permet pas d'ignorer | `RefuseCannotIgnoreAnUncheckedGroup` |
| Arrêt forcé uniquement après seconde confirmation | `ForceRequiresASecondClickAfterCooperativeFailure` |
| Erreur de détection distincte d'une liste vide | `DetectionFailureIsNotAnEmptySuccessfulScan` |
| Résultats, détection et Fermer traduits, fournisseur prioritaire, changement de langue à chaud | `DisplayedTextsUseCoreOrHostAndFollowLanguageChanges`, `CoreCloseTextAlsoAppearsOnSecondaryAction` |
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

`dotnet test -m:4` régénère 232 PNG Skia, sans fenêtre native, réseau ni processus réel
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
| Confirmation sans bloqueur | `Confirmation.png` |
| Aucun groupe coché | `NoneSelected.png` |
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
- `dotnet test -m:4` : 153 tests Mue, 29 tests WPF, 63 tests Avalonia en net8.0, 63 en net10.0 ;
  9 tests natifs Mac ignorés sous Windows, aucun échec.
- Aucun changement de source du cœur, de WPF ou de Velopack par rapport à `6819162`.
- Aucun build ni parcours du dépôt consommateur DockPad, aucune comparaison pixel de DockPad :
  le jalon de publication reste à effectuer dans sa session. Aucune connexion au Mac.

Le projet Avalonia supprime uniquement AVLN3001 : les fenêtres nécessitent les dépendances
du constructeur et ne sont pas instanciables par URI. Les ressources de thème restent
chargeables par URI. La construction et le test utilisent Avalonia 12.1.4 et xUnit v3,
requis par cette version d'Avalonia.Headless.XUnit ; les tests existants gardent xUnit v2.
