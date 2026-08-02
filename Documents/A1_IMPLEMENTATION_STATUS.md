# A1 Multiplayer & Networking — Implementation status

Updated: 2026-07-31

## Current verification caveat

The automated A1 harness and lifecycle fixes below were added after the earlier Unity evidence listed in this document. The project is now configured for Unity `6000.5.6f1`; a fresh compile/Test Runner pass for the latest changes is still pending because the project was locked by an open Unity instance during the first 6000.5 batch attempt. Therefore the new harness is `source-present` plus static-script-verified, not yet `unit-verified` or `runtime-verified`.

Current package baseline: NGO `2.13.0`, Unity Transport resolved as built-in `6.5.0`, Multiplayer Play Mode `2.0.2`, Multiplayer Tools `2.2.9`, and Multiplayer Services `2.2.4`. Their package metadata declares Unity `6000.0+` or `6000.3+` compatibility, so no blind package downgrade/upgrade is warranted.

## Implemented in code

- Session FSM with bounded/cancellable host, join, reconnect and scene-load operations.
- Protocol/build approval using an exact-size connection payload.
- Session-stable player identity, 128-bit reconnect credential, hash-only token storage, capacity reservation and 60-second expiry.
- Versioned reconnect snapshot for pose, life state, role schema and two authoritative weapon slots.
- Safe reconnect spawn validation, fallback search, new PlayerObject binding and restore acknowledgement before input.
- Graceful host shutdown reason and terminal host-loss cleanup; host migration remains deliberately out of scope.
- Unreliable movement command packets with two redundant commands, wrap-safe sequence handling, bounded input queue, ACK/replay and 100 ms input neutralization.
- Quantized movement/aim wire encoding. Fire commands reference a server-ACKed input sample; they do not carry client-selected origin/direction.
- Per-slot authoritative ammo/reload/cooldown/fire sequence state and RTT-aware 250 ms maximum rewind.
- Idempotent, rate-limited server pickup transaction with one winner and cached duplicate result.
- Tick-boundary server telemetry aggregation keyed by stable player identity.
- Common compact enemy locomotion/action state for Cop, Zombiegirl and Screamer, including late-join action phase filtering.
- Cop prefab `NavMeshAgent.baseOffset = 0`; the conflicting Cop `NetworkAnimator` component was removed.
- Development JSONL network diagnostics with per-session pseudonymized player identifiers.
- Multiplayer Play Mode `2.0.2` and Multiplayer Tools `2.2.9` are retained because their package metadata is compatible with Unity `6000.5.6f1`.
- Standalone one-host/three-client launcher and weak/severe/burst network simulation bootstrap.
- Existing long soak/performance tests are no longer marked `Explicit`.
- Enemy prefab grounding is normalized for Cop, Zombiegirl and Screamer (`NavMeshAgent.baseOffset = 0`).

## Local evidence currently available

- Runtime assembly compiles with Unity 6000.3's Roslyn response file.
- EditMode and PlayMode test assemblies compile with the same Unity toolchain.
- Historical Unity 6000.3.4f1 package resolution and NGO IL post-processing completed in earlier validation (`Logs/CodexA1BatchCompileElevated.log`); this is not evidence for the current Unity 6000.5.6f1 state.
- Full EditMode Test Runner: **259/259 passed**, including all 16 A1 hardening tests (`Logs/A1_EditModeResults5.xml`).
- Full PlayMode Test Runner: **21/22 passed**; the 5-minute enemy soak (302 s) and 60-second performance baseline passed. The sole failure was the pre-existing policy assertion requiring `[Explicit]`, which contradicted A1's requirement to enable those gates (`Logs/A1_PlayModeResults.xml`).
- Corrected A1 policy guard and verified it independently: **1/1 passed** (`Logs/A1_PlayModePolicyResults2.xml`).
- New EditMode policy tests cover connection payload sizing, sequence wrap, session concurrency/stale completion, reconnect boundary/capacity/token validation, pickup race/idempotency/rate-limit, telemetry tick aggregation, enemy action phase, weapon fire dedupe and lag-compensation windows.
- `git diff --check` must remain clean before handoff.

## Gates that must remain FALSE until evidence exists

The following checklist items are not complete merely because the implementation exists:

- Full Unity IL post-processing and complete EditMode/PlayMode Test Runner pass after UPM resolves the two newly pinned packages.
- 30-minute normal four-process run, weak/severe profiles and three-second outage report.
- 60-minute 30-enemy run and 15-minute 50-enemy stress run with bandwidth/main-thread/memory reports.
- Reconnect runs at 5/30/59/61 seconds and both graceful/forced host-loss runs.
- Final four physical machines/four real people map playthrough.

Do not change `Documents/checklist.md` A1 entries to `TRUE` until each entry links to its corresponding XML/JSONL/profile/playtest artifact.

## Standalone harness

After producing a Windows standalone build, run:

```powershell
.\BuildTools\A1\Run-A1FourPeer.ps1 -GameExecutable <path-to-game.exe> -DurationSeconds 1800 -Profile weak -BurstAtSeconds 300
```

The launcher creates one host plus three clients and writes isolated logs under `Logs/A1/<timestamp>/`. The shared coordination file contains only the Relay join code; reconnect credentials are never persisted there.
