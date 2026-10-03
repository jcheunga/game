# Battle ground materials

`ground-materials.png` is a new, lossless 1254 × 1254 material atlas generated with the built-in image generation tool. The exact request is preserved in `prompt.txt`; the tool delivered 1254 pixels despite the larger size requested. No upscaling or source-plate replacement was used.

Each cell is 418 × 418 pixels. Read left to right:

| Row | Materials |
| --- | --- |
| 1 | King's Road earth; forest soil; dry steppe |
| 2 | coastal stone; marsh peat; forge basalt |
| 3 | snow; cathedral stone; citadel slate |

`BattleTerrainCanvas` extracts only the active zone's material, builds mipmaps, and combines four offset samples with smooth blending to prevent obvious repeated or mirrored patterns. Detail fades into the stage's authored scenery at the playable perimeter. The 60 stage panoramas remain distinct and their imports now use lossless compression. Each scene is rendered uniformly, aligning the clear floor with the simulation and cropping perimeter scenery naturally instead of squashing it into strips. All movement and deployment bounds remain unchanged.

The battle UI uses icon-led health and courage at top left, gold at top right, individual deployment cards, and a bottom-left menu. Enemy wave counts and countdowns are hidden. The pause menu offers resume, ration-paid restart, game settings, and quit. Restart costs the same number of rations as entering the stage and is unavailable in shared matches.
