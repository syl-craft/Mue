# Historique

## Non publié

- Avalonia refuse d’ignorer les processus sans date de démarrage vérifiable et ferme les processus sélectionnés en parallèle. Les relais hors fournisseur Windows reçoivent une demande coopérative avant toute fermeture forcée.

- Avalonia reprend l’accord explicite, les groupes ignorés et le contrôle final par identité de b3 ; les textes provisoires sont remplacés par ceux du cœur. Tests headless et captures de confirmation, sans sélection et de scan incomplet.

- Projet `Mue.Avalonia` en Avalonia 12.1.4 pour .NET 8 et .NET 10 : fenêtres de mise à jour et de bloqueurs, indicateur autonome 40/26 px.
- Flux de confirmation, résultats de fermeture, ressources dynamiques et repli clair/sombre de faible priorité.
- Tests headless et captures des états dans `artifacts/avalonia/` ; ajout à la solution et au script d'empaquetage.

## 0.3.0 — 2026-11-07

- `MacUpdateBlockerFinder` utilise le contrat 0.2.0 : chemins multiples, inclusion facultative du processus courant et classifieur recevant la requête.
- Les processus Mac illisibles restent listés avec `Unavailable` et rendent le scan `Incomplete` ; l'adaptateur 0.1.0 est retiré.
- La fermeture Mac restitue les cinq résultats du contrat et accepte l'annulation, en conservant le signal lié à l'identité et le délai de cinq secondes.
- Un `Summary` non nul impose un accord explicite avant `PrepareAsync`, même sans bloqueur ou après une liste vidée par Réessayer. Sans résumé, le parcours reste inchangé.
- `Kill` et `AskToQuit` permettent d'ignorer les groupes décochés, y compris les processus illisibles. Le contrôle final tolère leurs seules identités PID/date ; les nouveaux processus restent bloquants. `Refuse` reste strict.
- Les textes fr/en deviennent génériques : `UpdateTexts.AppName` remplace `{AppName}` avant formatage dans les seules ressources de Mue. Les textes des hôtes restent prioritaires et intacts.
- Les aides `AskToQuit` annoncent la demande de quitter et le risque d'interruption sans enregistrement. Ajout des textes de confirmation, d'installation sans fermeture, de scan incomplet, de fermeture de fenêtre et des cinq résultats de fermeture (`UpdatePresentation.CloseResultText`).
- Pour les hôtes WPF : le constructeur de `UpdateBlockersDialog` et son rafraîchissement reçoivent un `UpdateBlockerScan`. `Incomplete` affiche un avertissement ; `IgnoredBlockers` expose les identités acceptées. Les hôtes `Refuse` sans résumé conservent leur parcours et leur rendu lorsque le scan est complet.

## 0.2.0 — 2026-11-06

- `ForcesShutdown` est remplacé par `ShutdownBehavior` : `Refuse`, `AskToQuit`, `Kill`.
- Textes fr/en intégrés et repli par clé ; `IUpdateTexts.TryGet` indique les traductions fournies par l'hôte.
- `IUpdatePendingStore`, stockage atomique `FileUpdatePendingStore` et `VerifyLaunch` pour constater succès ou retour arrière.
- `BlockerQuery` accepte plusieurs fichiers et dossiers ; `Find` retourne `UpdateBlockerScan`. Le classifieur reçoit la requête.
- Inspection Windows en accès limité, processus illisibles signalés par Restart Manager conservés avec `Unavailable`.
- `CloseAsync` accepte un jeton et retourne `UpdateBlockerCloseResult`, avec contrôle d'identité avant chaque arrêt.
- `IUpdateDialogHost.CheckAsync` vérifie sans préparer ; `PrepareAsync` ne s'exécute qu'une fois, après résolution des bloqueurs.
  Le résumé de vérification apparaît dans les deux dialogues ; absent, il n'occupe aucune place.
- `IUpdateBackend.CheckAsync` reçoit un jeton. Les recherches automatiques s'annulent lorsque la permission ou la préférence disparaît.
- Backend Velopack : requêtes annulables, `UserAgent`, option `LatestReleaseOnly` désactivée par défaut.
- `MueStyleKeys`, défauts de thème ajoutés explicitement avec `MueStyles.AddMissingDefaults`, contrôle autonome `UpdateBadge`.
- Tests de processus réels catégorisés `RealMachine` ; `pack.ps1 -Version` permet les préversions sans modifier le contrat publié.

## 0.1.0 — 2026-11-06

- Extraction du moteur de mise à jour de DockPad, de ses contrats d'hôte et de ses tests génériques.
- Paquets Mue, Mue.Velopack (Velopack 1.2.161) et Mue.Wpf pour .NET 8 et .NET 10.
- Dialogues WPF traduits par l'hôte et utilisant ses ressources de thème.
