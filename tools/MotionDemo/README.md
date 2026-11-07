# Clips de démonstration de Mue

Repris de DockPad `tools/MotionDemo` : chaque clip est une page HTML dont `render(t)` pose l'état exact de
l'instant t. `render.mjs` la capture image par image dans Chromium headless, puis ffmpeg produit le MP4
1080p 60 i/s et le GIF 960 px 20 i/s.

```
npm install
node render.mjs                                         # tout, clair et sombre, dans ../Mue-media/videos à côté du dépôt
node render.mjs --clip 02-blockers --theme dark
node render.mjs --clip 01-update-flow --stills 1.5,4    # images clés dans out/stills
node render.mjs --serve                                 # http://localhost:4174/clips/01-update-flow.html
```

| Fichier | Rôle |
|---|---|
| `shared/engine.js` | Timeline déterministe (copie de DockPad, barre de lecture ambre) |
| `shared/ui.js` | Appli hôte « Notebook », pastille, UpdatesDialog et UpdateBlockersDialog recopiés des XAML de Mue.Wpf 0.3.0 |
| `shared/mue.css` | Jetons `Brush.*` de Mue avec l'ambre du logo en accent |
| `clips/` | `01-update-flow`, `02-blockers`, `03-badge`, `04-confirmation` |

Les textes viennent de `Strings.resx` (0.3.0). Quand une fenêtre change dans Mue, mettre à jour `ui.js`
d'après le XAML, puis comparer une image clé à une capture de la vraie fenêtre.

Choix : la fenêtre des programmes à fermer fait 620 px de haut au lieu de 540, pour montrer trois groupes
sans défilement (la fenêtre est redimensionnable).
