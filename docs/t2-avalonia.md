# T2 — Port Avalonia

Base : `sylvain/b2-contrat-0-2-0`, commit `9fbd25e`. Travail isolé sur
`sylvain/t2-mue-avalonia`. Ni le contrat, ni Mue.Wpf, ni Mue.Velopack, ni la version 0.2.0
ne sont modifiés. Aucun paquet, tag ou push n'est produit.

## État de validation

Le port fonctionnel compile et ses tests passent. T2 reste **partiel** pour les textes
ci-dessous, absents du contrat. Aucun texte supplémentaire n'est introduit dans le cœur.

| Texte nécessaire | Observation sur le contrat 0.2.0 | Affichage actuel |
| --- | --- | --- |
| Fermer / Close | `Common_Close` est référencé par `UpdatePresentation`, absent des deux ressources Mue | `[Common_Close]` sans fournisseur hôte |
| Détection incomplète | `Update_IncompleteState` décrit une fermeture incomplète, pas une détection | Identifiant diagnostique `Incomplete` |
| Fermé, toujours ouvert, accès refusé, déjà terminé, identité modifiée | Aucune clé pour les cinq `UpdateBlockerCloseResult` | Identifiants distincts `Closed`, `StillRunning`, `AccessDenied`, `Gone`, `IdentityChanged` |
| D9, processus apparu après la détection | Les textes Kill/AskToQuit parlent des programmes encore ouverts ; Refuse n'explicite pas le refus de l'installeur | Textes existants sélectionnés selon `ShutdownBehavior` |

Pour terminer les textes strictement via `UpdateTexts` avec repli Mue fr/en, il faut faire
valider par la session DockPad l'ajout de `Common_Close`, d'une clé dédiée à la détection
incomplète, de cinq clés de résultat de fermeture et la précision des textes D9.
Ces ajouts ne sont pas réalisés ici. Une autre possibilité, soumise à l'utilisateur,
est une traduction privée à Mue.Avalonia ; elle n'est pas activée sans réponse.
Les mentions DockPad présentes dans les ressources intégrées restent également inchangées ;
l'hôte peut les traduire via `IUpdateTexts`.

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
| Résultat distinct pour chaque fermeture | cinq cas de `EveryCloseResultIsDisplayed` (identifiants, traductions bloquées) |
| Refus, annulation, nouveau bloqueur, échec d'application | `RefusalCancellationAndNewBlockerPreventApplication`, `ApplyFailureResumesHostAndKeepsDownloadedRelease` |
| Double clic et PID réutilisé | test de l'accord final et `CloseDoubleClickDoesNotRepeatOperationsAndIdentityChangeDropsOldSelection` |
| D9 selon capacité, processus indisponible visible | trois cas de `ShutdownBehaviorSelectsD9Texts` |
| Dictionnaire complet intact, seule clé absente ajoutée | `CompleteHostDictionaryIsUntouched`, `OnlyMissingKeysReceiveFallbackAndLaterHostDictionaryWins` |
| Thème à chaud et remplacement des ressources de l'hôte | `OpenWindowAndBadgeFollowThemeChangesAndHostReplacements` |
| Badge, clic dans les marges, clavier, commande | `BadgeHasFullyClickableFortyPixelTargetAndTwentySixPixelPill` |
| Repli fr/en existant et langue à chaud | `TextsUseMueFrenchAndEnglishFallbackAndFollowHostLanguageChange` |
| Clés de style identiques à WPF | `StyleContractMatchesWpfExactlyAndNoOtherDynamicKeyIsUsed` |

## Captures

`dotnet test -m:4` régénère 104 PNG Skia, sans fenêtre native, réseau ni processus réel
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

Les vues `-details` montrent le bas des listes, les chemins longs et les informations
indisponibles. Les libellés diagnostiques et `[Common_Close]` sont conservés dans les captures
pour rendre les limites visibles. À 440 × 360, la préparation réserve la priorité au résumé
et aux actions ; la zone des notes est réduite. À 560 × 480, plusieurs lignes restent visibles.

## Vérifications locales

- `dotnet build Mue.sln -m:4` : succès, zéro avertissement, zéro erreur.
- `dotnet test -m:4` : 86 tests Mue, 32 tests Avalonia en net8.0, 32 en net10.0 ; aucun ignoré.
- Aucun changement de source du cœur, de WPF ou de Velopack par rapport à `9fbd25e`.
- Aucun build ni parcours du dépôt consommateur DockPad, aucune comparaison pixel de DockPad :
  le jalon de publication reste à effectuer dans sa session. Aucune connexion au Mac.

Le projet Avalonia supprime uniquement AVLN3001 : les fenêtres nécessitent les dépendances
du constructeur et ne sont pas instanciables par URI. Les ressources de thème restent
chargeables par URI. La construction et le test utilisent Avalonia 12.1.4 et xUnit v3,
requis par cette version d'Avalonia.Headless.XUnit ; les tests existants gardent xUnit v2.
