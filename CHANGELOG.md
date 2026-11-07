# Historique

## 0.6.0 — 2026-11-07

- Crédit « Updates by Mue » dans `UpdatesDialog` (WPF et Avalonia) : plume de Mue et lien vers https://github.com/syl-craft/Mue, en bas à gauche, affiché par défaut, masquable avec `UpdatesDialogOptions.ShowCredit = false`. Nouvelle clé `Update_Credit` (89 clés), constante `UpdateCredit.Url`.
- Le lien a un style explicite (aucun style implicite de l'hôte ne s'y applique), s'atteint au clavier et porte un nom d'automatisation.
- Plume lisible à 3:1 au moins : tons du logo sur pied sombre, tons foncés (`#B35C0A`, `#C9700F`) sur pied clair, texte système en contraste élevé (WPF).
- Correctif 0.4.0 : en thème sombre géré par Mue, le libellé « Check for updates automatically » restait noir, car la recherche d'un style de case à cocher trouvait celui de Windows. Seules les ressources de l'application comptent désormais.
- README de vitrine, court et structuré (fonctionnalités, démonstrations en grille, démarrage rapide, paquets) ; le guide d'intégration complet passe dans `docs/guide.md` et `docs/guide.fr.md`.
- README : la ligne de commande s'appelle sur l'exe de `current\`, le lanceur stable Velopack n'attendant pas l'appli et ne rendant pas son code de sortie (constat DockPad).

## 0.5.0 — 2026-11-07

- Ligne de commande pour les scripts et les agents IA : `UpdateCommand.RunAsync(service, host, args, output, token)`, options `--check`, `--yes`, `--close-blockers`, `--restart`, `--json`, codes de sortie 0, 10, 20, 21, 22, 23 et 1. Même déroulé que la fenêtre, sans fenêtre ; le processus courant n'est jamais un bloqueur. JSON : `status`, `current`, `available`, `summary`, `blockers` (`name`, `pid`, `detail`, `relay`, `group`), `message`.
- `IUpdateBackend.Apply(release, silent, restart)`, méthode par défaut qui renvoie à `Apply(release)` ; `UpdateService.Apply(silent, restart)`. `VelopackBackend` transmet `silent` et `restart` à Velopack ; la fenêtre garde `Apply()` (avec fenêtre de progression, relancement).
- README : première installation silencieuse (`Setup.exe --silent`, vérifié sur Velopack 1.2.161), formes PowerShell et cmd vérifiées pour attendre une appli WinExe, attente de la fin de la copie après le code 0. macOS : mode silencieux non vérifié sur une vraie machine.
- `samples/Mue.Sample.Wpf` : verbe `update` ; console du parent attachée seulement sans redirection.
- Revue Codex : processus courant exclu même si le backend l'inclut, un seul JSON sur toutes les issues (exceptions comprises), recherche refusée ou en cours jamais annoncée « à jour », annulation contrôlée à chaque étape.

## 0.4.1 — 2026-11-07

- `VelopackBackend` sans `VelopackApp.Run` ni locator explicite (tests qui lancent l'appli complète) : construction sans exception, `IsInstalled` faux (état `Unavailable`), `CurrentVersion` = `FallbackVersion`, aucune recherche ni requête réseau, y compris par le planificateur automatique. `DownloadAsync` et `Apply` lèvent `InvalidOperationException`. Rien ne change avec un locator.

## 0.4.0 — 2026-11-07

- Textes : `UpdateTextKeys.All` et `UpdateTextKeys.Missing(IUpdateTexts)`. Une langue n'entre que complète (test). Trois clés : `Update_ConfirmHelpForced`, `Update_ConfirmHelpAskToQuit`, `Update_Download`.
- Styles : liste unique `Mue.UpdateStyleKeys` (`Brushes`, `Styles`, `Required`) ; `Mue.Wpf.MueStyleKeys` et `Mue.Avalonia.MueStyleKeys` y renvoient. Enum `MueTheme { System, Light, Dark }`.
- Mue.Wpf : jeu sombre par défaut, `AddMissingDefaults(app, theme)`, `SetTheme(app, theme)` et `SetTheme(app, null)`, barre de titre sombre, `ManageTitleBar`, événement `WindowOpened`. Sans appel de l'hôte, aucun changement de ressource, de barre de titre ni d'abonnement. Mue ne remplace que les valeurs qu'il a ajoutées. Contraste élevé : couleurs système, barre de titre laissée à Windows.
- **Changement de comportement** : `AddMissingDefaults(app)` vaut `System` (clair seul en 0.3.0) et gère la barre de titre ; `ManageTitleBar = false` la laisse à l'hôte.
- Confirmation : avec `Kill` ou `AskToQuit`, l'aide reste visible (WPF et Avalonia).
- `UpdatesDialogOptions` (WPF et Avalonia) : `SeparateInstallStep` (téléchargement puis installation en deux clics) et `ShowBlockers`. Constructeurs 0.3.0 conservés ; l'appel littéral `new UpdatesDialog(service, host, null)` devient ambigu à la compilation (source seulement).
- Tests de contraste WCAG AA des jeux par défaut, WPF et Avalonia, clair et sombre, y compris les boutons survolés et enfoncés. Le rouge sombre par défaut passe de `#FF9B91` à `#FFB4AB` (4,3:1 sur un bouton danger enfoncé).
- Mue.Wpf range ses valeurs par défaut dans un dictionnaire fusionné en premier : une palette de l'hôte chargée après `AddMissingDefaults` l'emporte.
- La liste commune s'appelle `Mue.UpdateStyleKeys`, pour ne pas rendre ambigu `MueStyleKeys` chez un hôte qui importe `Mue` et `Mue.Wpf`.
- `samples/Mue.Sample.Wpf` : appli d'exemple et captures neutres (`--capture`), hors paquets.
- README en anglais (celui des paquets, images en liens absolus) et en français (`README.fr.md`), avec bannières, démarrage rapide compilé contre les paquets 0.3.0 et démonstrations animées hébergées dans le dépôt média `Mue-media`.
- Icône des paquets (`docs/images/mue-icon.png`, logo plume ambre) : `PackageIcon`.
- `tools/MotionDemo` : rendu des démonstrations animées (Chromium headless et ffmpeg), écrit dans `../Mue-media/videos`.

## 0.3.0 — 2026-11-07

- Nouveau paquet `Mue.Avalonia` (Avalonia 12.1.4, .NET 8 et .NET 10) : fenêtres de mise à jour et de bloqueurs, indicateur autonome 40/26 px, ressources dynamiques et repli clair/sombre de faible priorité. Même parcours que `Mue.Wpf`.
- Détection macOS : `MacUpdateBlockerFinder` (macOS 15+) sur le contrat 0.2.0. Un processus dont le chemin est illisible est ignoré, comme sous Windows sans signalement du Restart Manager ; un exécutable de l'appli dont l'identité est illisible reste `Unavailable`. La fermeture restitue les cinq résultats du contrat, accepte l'annulation et garde le signal lié à l'identité, avec un délai de cinq secondes.
- Un `Summary` non nul impose un accord explicite avant `PrepareAsync`, même sans bloqueur ou après une liste vidée par Réessayer. Sans résumé, le parcours reste inchangé.
- `Kill` et `AskToQuit` permettent d'ignorer les groupes décochés. Le contrôle final tolère leurs seules identités PID + heure de démarrage ; les nouveaux processus restent bloquants. Un bloqueur sans heure de démarrage ne peut jamais être ignoré : il faut le fermer. `Refuse` reste strict.
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
