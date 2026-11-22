# Weapon sounds

## In use (since 2026-11-15): `<Weapon>/Fire/` and `<Weapon>/Handling/`

Supplied by the user in `E:\Audio\SFX\Weapons\<Weapon>\{Fire,Handling}` (Valorant-named game audio, kept for this
private, non-published education project only; not CC0, do not ship publicly). Built by
`Reports/Audio-2026-11-15/scripts/build_guns.py` (Blender audaspace + numpy):

- `<weapon>_fire_NN.wav`: the gun's fire layers mixed (body + LFE / mechanical layer), trimmed below -60 dB,
  peak -1 dBFS. Warden: 4 mixes of FP transient + tail + LFE + mechanical.
- `<weapon>_fire_far_NN.wav`: distant takes. Warden uses its own `TP_Distant` recordings; the others are the near mix
  low-passed (1.4 kHz, 24 dB/oct) with a short reflection smear, peak -3 dBFS.
- `warden_fire_tp_NN.wav`: Warden's third-person close takes, heard for other players' shots (`remoteClips`).
- `Handling/*.wav`: foley copied unchanged (authored levels kept), only the files a cue uses. The Bucky shell-in
  takes have their pre-roll cut so the click lands 5 ms in.

Cue times live in `../../Editor/WeaponFoley.json` (Tools/FPS/Audio/Build All writes them into the WeaponData assets).

## Still CC0 and in use

| Files | Source | Author | License |
|---|---|---|---|
| `Shared/dry_fire_*` | Kenney "Impact Sounds" 1.0, `impactMetal_light_000..002.ogg`, https://kenney.nl/assets/impact-sounds | Kenney (www.kenney.nl) | CC0 |

## Removed 2026-11-15 (user's decision)

The earlier CC0 gun takes (Free Firearm Sound Library fire/far shots, SpringySpringo reloads, zer0_sol shotgun
shells/rack) were replaced by the sounds above and moved to the Windows Recycle Bin: 74 WAV files with their metas.
