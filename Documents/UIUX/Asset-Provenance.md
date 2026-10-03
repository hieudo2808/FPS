# Gameplay UI asset provenance

| Source | Local usage | License/permission | Credit | Notes |
|---|---|---|---|---|
| Lotus Garden — Post Apocalypse Survival UI | Existing curated panels, bars and slots | Personal/commercial use; modification allowed; no resale/redistribution | Not required by page | Reuse existing imported copies; do not duplicate |
| SunGraphica — Horror Game UI Creator Kit | Existing horror frames/icons and future file-reader accents | Personal/commercial use; no redistribution | `SunGraphica` | Use the free package only unless separately authorized |
| UIForge — Crimson Dread UI Kit | Optional missing badges/tabs/slots | Personal/commercial use; no credit required | None | Keep as a fallback family, not a mixed primary skin |
| `E:/ProjectSettings/Assets` | Authorized realistic key-item/document icons | User-authorized for this project | Preserve source attribution if later supplied | Import source textures only; do not copy unrelated Unity scripts or metadata |

## Import rule

Revalidated publisher terms on 2026-09-23. Required in-game/credits attribution: **UI assets by SunGraphica**. Existing curated UI destination: `Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/`. Authorized supplied icons destination: `Icons/Mission/` underneath that directory, with source mapping BlueCard → B2AccessCard, BatteryPart1 → ServiceFuse, VirusSample → EvidenceCase, Notebook → Notebook, DataBus → DataBus. Source permission is the user's authorization, not a verified redistribution license; do not distribute source packs separately.

Keep original source files outside the runtime catalog, import only the textures/sprites needed by the project, and record any subsequent modification in the table above. Do not treat “educational” or “non-public” as a substitute for a license for newly found web assets.

## Campaign world reuse — 2026-09-27

- Physical Files reuse the existing `Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Paper.prefab`, including its original mesh and paper material. No external download or hand-drawn document icon was introduced. All 21 sources retain the original prefab reference; text lives in the catalog, not the paper texture.
- The morgue expansion uses cloned structural render/collision meshes in `Assets/FPS/Features/World/Content/Campaign/MorgueArena/`. Original Asylum meshes are retained. The receiving partition uses local variants of the existing `Wall_4` and `Tiles_10` materials and their existing albedo maps; shared originals are unchanged.
- These are already-integrated project assets. This entry records reuse and modifications, not a new independent verification of the original environment-pack redistribution license. Keep their original attribution/license records with project backups; do not redistribute source packs separately.

## Approved prop integration — 2026-09-28

Current asset mapping, RPaciorek source/license location, reused CRT/keyboard/paper/cabinet assets, actual Unity captures and outstanding imports are tracked in [the implementation record](Prop-Approval-2026-09-27/Implementation-2026-09-28.md). Proposal previews are not proof of integration. No newly drawn item art was added.
