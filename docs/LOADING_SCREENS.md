# Loading artwork slots

The game now shows an illustrated loading layout for battle entry and return, with a live title and tip. Existing zone pictures are placeholders until final illustrations are supplied.

Add final artwork under `assets/ui/loading/`. Use a full-bleed 16:9 picture, ideally 1920 × 1080 or larger. Keep the main subject near the center/right; the game places its title at the top-left and tips along the lower-left over a soft shade. Do not bake lettering into the image. Wider/narrower screens crop the edges while preserving aspect ratio.

| File | Zone |
| --- | --- |
| city.png | King's Road |
| harbor.png | Saltwake Docks |
| foundry.png | Emberforge March |
| quarantine.png | Ashen Ward |
| thornwall.png | Thornwall Pass |
| basilica.png | Hollow Basilica |
| mire.png | Mire of Saints |
| steppe.png | Sunfall Steppe |
| gloamwood.png | Gloamwood Verge |
| citadel.png | Crownfall Citadel |
| default.png | Optional fallback for any missing zone illustration |

Zone illustrations take priority over `default.png`, then the existing zone mission picture. No generated illustration is required for the code to work. To preview the layout with current pictures, run the production polish UI review with an isolated save; its captures are under `artifacts/production-polish/desktop/` and `phone/`.
