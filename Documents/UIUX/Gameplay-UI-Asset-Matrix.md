# Gameplay UI asset matrix

Phase 1 asset audit for the campaign squad HUD, debrief and Team Inventory.

## Revalidation — 2026-09-23

Google search was attempted first; the HTTP response required browser JavaScript and did not expose usable search results. The three named publisher pages were then fetched directly and their license statements verified. No new web package was downloaded.

Contact sheets: `Campaign-Assets-1920x1080.png` and `Campaign-Assets-1280x720.png` (rebuild with `Render-CampaignAssetSheet.ps1`). These are asset comparison sheets, not gameplay screenshots.

All campaign screens use the existing Lotus/SunGraphica curated family. Crimson Dread remains unused. The supplied `Notebook` is a laptop icon, not a paper notebook: it represents a digital file reader. `EvidenceCase` is a sample-vial illustration representing sealed evidence, not literal case geometry.

Verified Unity import settings: curated frames/bars use Sprite/Single, no mipmaps, max size 1024, uncompressed; mission icons use Sprite/Single, no mipmaps, max size 512, high-quality compression. Runtime icons preserve aspect ratio. No duplicate imports were made.

| Target | Primary source | Fallback | Status |
|---|---|---|---|
| HUD panels, health bars, hotbar, modal plates | Existing Lotus Garden/SunGraphica subset in `Assets/FPS/Features/UI/Content/Sprites/Survival/Curated` | Crimson Dread UI Kit | Reuse existing project assets |
| Key-card icons | `E:/ProjectSettings/Assets/Resources/inventory/icons/BlueCard.png`, `RedCard.png`, `GreenCard.png` | Existing mission icon sprites | Candidate verified visually |
| Fuse/power item | `E:/ProjectSettings/Assets/Resources/inventory/icons/BatteryPart1.png` | `Battery1.png` | Candidate verified visually |
| Evidence/sample | `E:/ProjectSettings/Assets/Resources/inventory/icons/VirusSample.png`, `DataBus.png` | Existing mission icons | Candidate verified visually |
| Notes/books/files | `E:/ProjectSettings/Assets/Resources/inventory/icons/Notebook.png` and Notebook parts | Document icon from current UI kit | Candidate verified visually |
| Squad portraits | Render current Brimstone/Clove/Gekko/Sage prefabs with a fixed editor camera | Character-neutral status icon | No hand-drawn portraits |
| Downed/dead/disconnected badges | Existing BloodFX/HUD/status sprites | Crimson Dread badge/arrow assets | Select one cohesive set |

## Web sources checked

- Lotus Garden, Post Apocalypse Survival UI: https://lotus-garden.itch.io/post-apocalypse-survival-ui-asset-pack
- SunGraphica, Horror Game UI Creator Kit: https://sungraphica.itch.io/creepy-game-ui
- UIForge, Crimson Dread UI Kit: https://uiforge.itch.io/crimson-dread-ui-kit

Lotus Garden permits personal/commercial use and modification but prohibits resale or redistribution. SunGraphica permits personal/commercial use with credit to `SunGraphica` and prohibits redistribution. UIForge states personal/commercial use is allowed without credit. The supplied `E:/ProjectSettings/Assets` folder is used under the user's stated authorization.

The implementation must keep the source URL, author, license/permission, credit requirement and destination path in the provenance record before importing any new external file.
