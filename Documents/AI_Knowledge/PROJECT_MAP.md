# FPS project map for agents

This is a compact orientation aid. It is intentionally not a replacement for graph queries or source inspection.

## Established structure

`Assets/FPS/FPS.asmdef` is the main runtime assembly. It references Netcode for GameObjects, Input System, TextMesh Pro, UniBT, Unity Services, and Unity Collections.

The project also has these assembly boundaries:

- `Assets/ThirdParty/UniBT/Scripts/Runtime/UniBT.asmdef` — third-party behavior-tree runtime.
- `Assets/ThirdParty/UniBT/Scripts/Editor/UniBT.Editor.asmdef` — behavior-tree editor code.
- `Assets/Editor/FPS.AIKnowledge.Editor.asmdef` — editor-only knowledge exporter.

Feature folders under `Assets/FPS/Features` are `AI`, `Audio`, `Characters`, `Input`, `Interaction`, `Missions`, `Networking`, `UI`, `Weapons`, and `World`.

## World content layout (2026-09-13)

Environment content belongs under `Assets/FPS/Features/World/Content`. Organize by facility or reusable world category, then by asset type. Avoid top-level vendor/import folders or folders named after a repair/review task.

- `AsylumFacility` and `ExperimentFacility`: `Materials`, `Models`, `Prefabs`, `Textures`, `Data`, and `Scenes`; generated Asylum meshes belong in `AsylumFacility/Meshes`.
- Facility authoring scenes and their bake folders live together in each facility's `Scenes` folder. Imported showcase scenes and their bake data live in `Scenes/Source`.
- `ExperimentFacility/Prefabs/Source` holds original imported prefabs. Project-authored prefabs remain in `Prefabs`, including same-name assets with distinct GUIDs. Do not overwrite one with the other.
- Asylum exterior-face meshes: `AsylumFacility/Meshes/ExteriorFaces`; architecture additions: `AsylumFacility/Meshes/Architecture`.
- Shared concrete meshes: `Meshes/Concrete`; concrete/safety/wayfinding materials: `Materials`.
- Hangar collision meshes: `Buildings/Industrial/Hangars/Hangar_v*/Collision`.
- Building stairs: `Buildings/Industrial/Meshes/Stairs`; service shed: `Buildings/Industrial/ServiceShed`.
- Factory paving/yard: `Roads/FactoryYard/Meshes`; road junctions: `Roads/Road_sets/FactoryRoads`.
- Fence meshes: `Fences/Meshes`; fuel-tank pads: `Oil_tanks/Meshes/Pads`; inter-facility terrain: `Campaign/Terrain`.

The active build scenes remain `Assets/FPS/Scenes/MainMenu.unity`, `LobbyScene.unity`, and `GameScene.unity`. Move assets through AssetDatabase to retain GUIDs and update this map when ownership/layout changes.

## Likely entry points (inference, verify with the code graph)

- Networking/session lifecycle: `NetworkGameManager`, `NetworkMatchStateManager`, `NetworkSpawnManager`, `PlayerSessionRegistry`.
- AI/spawning: `AIDirector`, `DirectorSpawnService`, `DirectorSpawnAnchor`, `DirectorZone`, `AttackSlotManager`.
- Enemy lifecycle: `ZombieFactory`, `ZombiePoolManager`, `ZombieRegistry`, `SpecialInfectedRegistry`, `EnemyAI`, `EnemyHealth`.
- Player/combat: `PlayerMovement`, `PlayerHealth`, `PlayerInfectionController`, `WeaponManager`, `WeaponFireHandler`, `InteractionManager`.
- UI/input/audio support: `HUDManager`, `SettingsManager`, `InputManager`, `AudioManager`.

These names indicate likely responsibilities from the current file layout; they do not prove runtime ownership, startup order, or network authority. Query callers/callees and inspect the relevant prefab/scene before editing.

## Known graph caveats

- `ZombieFactory.cs:46` is parse-partial in the current C# graph.
- Unity serialized assets are intentionally excluded from the C# graph and must be inspected through Unity-Skills/Gerty or the semantic graph.
- Dynamic calls, reflection, prefab overrides, runtime-instantiated objects, and Animator transition behavior are not fully represented.
