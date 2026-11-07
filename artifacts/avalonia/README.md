# Captures T2 Avalonia

Voir [le rapport de validation](../../docs/t2-avalonia.md) pour la matrice et les
textes fournis par le cœur b3.

Les dossiers net8.0 et net10.0 contiennent chacun quatre variantes : clair et sombre,
440 × 360 et 560 × 480. Chaque variante contient 29 captures, soit 232 PNG au total :
les 13 vues initiales et 14 vues supplémentaires des capacités AskToQuit/Refuse,
des échecs coopératifs et des cinq résultats de fermeture, puis les états
`Confirmation.png` (sans bloqueur) et `NoneSelected.png` (aucun groupe coché).

Génération : `dotnet test -m:4`. Rendu Skia d’Avalonia.Headless, à l’échelle 1.
Notes de version et chemins longs fictifs ; aucun processus de l’utilisateur dans les fixtures.
Les tests vérifient le texte affiché et la disposition de l’avertissement de perte de données.
Les captures sont inspectées visuellement, sans comparaison pixel automatisée.
Aucune capture n’est une preuve du jalon consommateur DockPad.
