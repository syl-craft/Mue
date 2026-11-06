# Captures T2 Avalonia

Voir [le rapport de validation](../../docs/t2-avalonia.md) pour la matrice et les limites de traduction.

Les dossiers net8.0 et net10.0 contiennent chacun quatre variantes : clair et sombre,
440 × 360 et 560 × 480. Chaque variante contient les onze états demandés et deux vues
supplémentaires du bas des listes de processus, soit 104 PNG au total.

Génération : `dotnet test -m:4`. Rendu Skia d’Avalonia.Headless, à l’échelle 1.
Notes de version et chemins longs fictifs ; aucun processus de l’utilisateur dans les fixtures.
Les captures montrent les libellés effectivement disponibles dans le contrat 0.2.0,
y compris les manques documentés. Aucune capture n’est une preuve du jalon consommateur DockPad.
