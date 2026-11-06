# Historique

## Non publié — T2 Avalonia

- Projet `Mue.Avalonia` en Avalonia 12.1.4 pour .NET 8 et .NET 10 : fenêtres de mise à jour et de bloqueurs, indicateur autonome 40/26 px.
- Flux de confirmation, résultats de fermeture, ressources dynamiques et repli clair/sombre de faible priorité.
- Tests headless et captures des états dans `artifacts/avalonia/` ; ajout à la solution et au script d'empaquetage.
- Validation T2 partielle : textes absents du contrat 0.2.0 recensés dans `docs/t2-avalonia.md`, sans modification du cœur ni de WPF.

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
