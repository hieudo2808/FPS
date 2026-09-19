# Survival horror UI artwork

17 selected sprites, organized by function:

- **Surfaces**: opaque distressed panel and subtle wear overlay (SunGraphica).
- **Controls**: 9-sliced control plate and menu focus (SunGraphica).
- **Bars**: cropped health frame, fill and track (lotus_garden).
- **Icons**: menu, settings, lock and check (SunGraphica); HP, biohazard and grenade (existing ProjectSettingsPack).
- **Effects**: low-health blood edge (existing ProjectSettingsPack).
- **Licenses**: source terms and provenance notes.

Original ZIP/PSD libraries remain under `Documents/UIUX/FreeAssets`. Gamanbit, unrelated crafting icons, bright variants and duplicated resolutions are not imported into the runtime UI.

Sprite references are serialized by `TacticalUiWorkshop`. Import settings are owned by `SurvivalUiArt`. Run `Tools > FPS > UI > Apply Survival Horror Design` only when intentionally reauthoring all three UI scenes/shared prefabs. The operation saves and preserves the active scene, but reapplies authored UI layout values.

Source crops, transformations and hashes: `Documents/UIUX/curated-assets.json`. Reproduction script: `Documents/UIUX/Tools/prepare_survival_assets.py`. It only overwrites the selected derived PNGs. Save all open Unity scenes and assets before running it.

Credit included in Settings: **UI art: SunGraphica / lotus_garden**.
