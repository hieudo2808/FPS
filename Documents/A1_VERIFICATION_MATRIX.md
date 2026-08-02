# A1 Automated Verification Matrix

This file records machine-verifiable evidence. It does not replace `Documents/checklist.md` and does not mark manual gameplay requirements complete.

Final artifact environment: Unity `6000.5.6f1`, Netcode for GameObjects `2.13.0`, Multiplayer Tools `2.2.9`, and Multiplayer Play Mode `2.0.2`. The earlier plan baseline listed NGO `2.9.2`/Unity `6000.3.4f1`; the project was upgraded to the versions above and the final build/artifacts record the effective versions explicitly.

## Automated gates

| Check | Artifact | Current status |
|---|---|---|
| Four-process Relay startup and player spawn | `Logs/A1/20260801-133754-315/summary.json` | `runtime-verified` - pass, exit 0; final-build smoke |
| Final-build four-process Relay smoke | `Logs/A1/20260801-213150-874/summary.json` (managed hash `04DBE2DD...`, probe hash `CD998F55...`) | `runtime-verified` - Unity 6000.5.6f1 build pass; 4 peers/4 players, metrics, zero warnings/errors |
| Final-build artifact contract | `Logs/A1/20260801-213150-874/summary.json` and `run-manifest.json` | `runtime-verified` - pass, `event` schema, manifest hash, binary hashes and package versions present; zero warnings/errors |
| Runtime PlayerPrefab registration | `prefab_registration` events in `Logs/A1/20260801-133754-315/` | `runtime-verified` - pass |
| Lobby/GameScene synchronization | `lobby_ready`, `game_scene_ready` events in `Logs/A1/20260801-133754-315/` | `runtime-verified` - pass |
| Reconnect success boundaries | `Logs/A1/20260801-213549-722/summary.json`, `Logs/A1/20260801-213648-769/summary.json`, and `Logs/A1/20260801-211946-347/summary.json` | `runtime-verified` - reservation ages 5s, 30s and 59s pass on the final probe hash; restore ACK, final four-player snapshot and no Deferred OnSpawn warning |
| Reconnect expiry boundary at 61 seconds of reservation age | `Logs/A1/20260801-212202-806/summary.json` | `runtime-verified` - disconnect 5s/reconnect 66s (61s elapsed age); `ReconnectExpired`, no reconnect completion, zero warnings/errors |
| Reconnect absolute-time semantic guard | `Logs/A1/20260801-205015-971/summary.json` | `runtime-verified` - disconnect 5s/reconnect 61s is a 56s age and therefore succeeds; prevents false expiry evidence |
| Graceful/forced host loss | `Logs/A1/20260801-124242-690/` and `Logs/A1/20260801-125958-027/summary.json` | `runtime-verified` - pass; all clients clean up |
| Weak/severe/burst network profiles | `Logs/A1/20260801-122721-709/`, `Logs/A1/20260801-131810-957/`, `Logs/A1/20260801-131919-382/summary.json` | `runtime-verified` - pass; warning/error gate and metrics pass |
| Final-build normal/weak/severe/burst smoke | `Logs/A1/20260801-210101-642/summary.json` plus prior profile runs `Logs/A1/20260801-164644-722/`, `Logs/A1/20260801-164914-424/`, `Logs/A1/20260801-165142-873/` | `runtime-verified` - normal passes on current managed/probe build; weak/severe/burst pass on immediately preceding gameplay/network build, metrics and zero warnings/errors |
| 50 enemy normal stress | `Logs/A1/20260801-134130-587/summary.json` | `runtime-verified` - pass, exit 0; 363 metric windows |
| 50 enemy weak stress | `Logs/A1/20260801-135732-392/summary.json` | `runtime-verified` - pass, exit 0; 363 metric windows |
| 50 enemy severe stress | `Logs/A1/20260801-141252-802/summary.json` | `runtime-verified` - pass, exit 0; metrics and warning/error gate pass |
| 30 enemy 60-minute baseline stress | `Logs/A1/20260801-142817-978/summary.json` | `runtime-verified` - pass, exit 0; 363 metric windows, zero warnings/errors |
| Final-build graceful/forced host loss | `Logs/A1/20260801-165423-682/summary.json` and `Logs/A1/20260801-165521-332/summary.json` | `runtime-verified` - both pass on the immediately preceding gameplay/network build; current build delta is probe/diagnostics only |
| NetworkVariable lifecycle test seam | `Logs/A2-editmode-final-reconnect-allowance.xml` - `NetworkMatchState_TestSeamDoesNotRequireSpawnedNetworkObject` | `unit-verified` - pass, 278/278 |
| AudioListener default lifecycle invariant | `Logs/A2-editmode-final-reconnect-allowance.xml`, `Logs/A2-playmode-final-reconnect-allowance.xml`, and `Logs/A1/20260801-213150-874/summary.json` | `unit/runtime-verified` - pass; no missing-listener warning in final PlayMode or current Relay run |
| UniBT SerializeReference metadata | `Logs/A2-editmode-final-reconnect-allowance.xml`, `Logs/A2-playmode-final-reconnect-allowance.xml`, and `Logs/A1/20260801-213150-874/summary.json` | `unit/runtime-verified` - pass; no SerializeReference warning in final PlayMode or current Relay run |

## Adaptive Director runtime gate

| Check | Artifact | Current status |
|---|---|---|
| Adaptive four-peer Relay session | `Logs/A2/20260801-213251-393/summary.json` | `runtime-verified` - pass on final build, exit 0, 11/11 checks |
| Adaptive phase trace and snapshots | `director_phase_changed`, `adaptive_state` in `Logs/A2/20260801-213251-393/` | `runtime-verified` - pass |
| Relax-boundary dynamic evaluation | `difficulty_evaluated` in `Logs/A2/20260801-213251-393/` | `runtime-verified` - pass |
| Static difficulty golden behavior | `Logs/A2-editmode-final-reconnect-allowance.xml` and `DifficultyManagerTests` | `unit-verified` - pass |

## Evidence levels

- `source-present`: implementation exists in the working tree.
- `unit-verified`: EditMode or PlayMode test passed.
- `runtime-verified`: standalone/Relay artifact passed all required checks.
- `manual-verified`: real people, real machines, or subjective gameplay review completed.

The working tree is intentionally not cleaned or committed by the verification harness. Every run records git status, HEAD, package hashes, scene/prefab hashes, executable hash, managed assembly hashes, package versions and manifest hash in `run-manifest.json`.

Final-build smoke, Relay baseline/impairment/reconnect/host-loss, stress, and A2 runtime artifacts are recorded above. The prior intermittent Deferred OnSpawn artifact is retained as diagnostic history, not evidence of completion; reconnect artifacts after the server pre-disconnect replication drain pass without that warning.

The 30-enemy 60-minute baseline artifact is retained as a pass from the same gameplay/network source before the diagnostic-only logging optimization; the current build has a final-build Relay smoke, targeted reconnect boundaries, adaptive smoke and the full EditMode/PlayMode gate. Reconnect, impairment, host-loss and stress artifacts remain valid for the same gameplay/network source; the later build delta is event schema, diagnostic logging, expected-failure classification and a five-second server transport allowance. Manual gameplay/visual/audio checks remain pending. `Documents/checklist.md` remains the source of truth for checklist booleans and is not changed by this matrix.
