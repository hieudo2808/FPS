# FPS Project Structure

This directory is the first-party project boundary. Third-party packages and
vendor assets stay outside it under `Assets/ThirdParty` or their package roots.
`Assets/DefaultNetworkPrefabs.asset` is the one first-party exception: Netcode
for GameObjects regenerates its default list at that root convention.

## Layout

```text
FPS/
|-- Core/Runtime/                 Shared, feature-agnostic runtime primitives
|   |-- Combat/
|   |-- Diagnostics/
|   |-- Lifecycle/
|   `-- Pooling/
|-- Features/
|   |-- AI/                       Adaptive director and encounter AI
|   |-- Audio/                    Audio runtime and authored audio content
|   |-- Characters/               Player, enemy, and character animation slices
|   |-- Input/                    Input routing
|   |-- Interaction/              Interaction contracts and pickup flow
|   |-- Networking/               Sessions, replication, and simulation tools
|   |-- UI/                       UI runtime and authored UI content
|   |-- Weapons/                  Weapon runtime, data, and authored content
|   `-- World/                    World rendering and environment content
|-- Config/                       Cross-feature authored configuration
|-- Editor/                       Editor-only tooling
|-- Generated/                    Rebuildable generated assets
|-- Scenes/                       Composition roots and build scenes
|-- Tests/                        EditMode and PlayMode regression suites
`-- FPS.asmdef                    First-party runtime assembly boundary
```

## Dependency rules

1. `Core` must not depend on a feature.
2. A feature owns its runtime code, data, and content whenever practical.
3. Cross-feature calls should use explicit contracts; do not move convenience
   code into `Core` unless at least two features genuinely share it.
4. `Editor` may depend on runtime code, but runtime code must never depend on
   `Editor`.
5. Tests may depend on runtime features. Production assemblies must not depend
   on tests.
6. Third-party code is not edited or moved into the first-party boundary.

The runtime currently uses one `FPS` assembly because the existing gameplay,
AI, networking, and weapon systems have bidirectional type dependencies.
Creating one assembly per folder now would introduce circular assembly
references or require a risky framework-sized refactor. Add feature asmdefs only
after contracts make the dependency graph directional; the network simulation
slice remains separately compiled because it already has a clean boundary.
