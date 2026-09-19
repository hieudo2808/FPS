# Knowledge routing benchmark

Use these small questions after restarting the agent client. A pass means the agent reaches the right knowledge surface before opening many files.

| Question | First surface | Expected evidence |
|---|---|---|
| Who participates in zombie spawning and pooling? | Codebase Memory | Search/trace around AIDirector, ZombieFactory, ZombiePoolManager, and ZombieRegistry; then read the candidate files. |
| What is attached to the Clove player prefab? | Unity-Skills/Gerty | Query the prefab asset and report components/hierarchy; do not parse prefab YAML. |
| What objects are in the active MainMenu scene? | Unity-Skills/Gerty | Query the current scene and report roots/components; do not scan all scenes. |
| What can be affected by changing Weapon.FireBullet? | Codebase Memory | Inspect inbound callers/overrides and then confirm any serialized Unity references. |
| Is the code graph complete for ZombieFactory.cs? | Codebase Memory coverage | Report the known parse-partial range at line 46 and qualify conclusions. |

## Recorded smoke checks

- search_graph("Zombie") returned 30 matches, including AIDirector.SpawnZombie, ZombieFactory.SpawnZombie, ZombiePoolManager.GetZombie, and ZombieRegistry.GetRandomZombie.
- search_graph("Weapon") returned 236 matches and ranked Weapon.ReloadWeapon, Weapon.FireBullet, and related lifecycle methods.
- Coverage for Assets/FPS returned known_gaps only for ZombieFactory.cs:46; the result is explicitly best-effort.
- Unity semantic export completed with 0 warnings and preserved the active MainMenu.unity scene as clean.
- Final Codebase Memory index completed with 14,390 nodes and 52,202 edges.

These are routing checks, not proof that the indexes understand runtime behavior. Re-run them after changing indexing rules, Unity packages, or assembly boundaries.
