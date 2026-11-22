# Player sounds

The generic sounds below derive from CC0 sources, downloaded 2026-11-14 with the user's approval; split into single calls,
trimmed, faded, peak-normalised, mono 16-bit WAV. Crediting the authors is optional.

| Files | Source | Author | License |
|---|---|---|---|
| `Voice/Male/male_{hurt,downed,death}_NN.wav` | "grunts of male death and pain", `death pain grunts.wav`, https://opengameart.org/content/grunts-male-death-and-pain | thebardofblasphemy | CC0 |
| `Voice/Male/male_strain_NN.wav` | "15 vocal male strain/hurt/pain/jump sounds", `slightscreams.7z` (slightscream-01..06.flac), https://opengameart.org/content/15-vocal-male-strainhurtpainjump-sounds (CC0 from 2024-08-30) | qubodup | CC0 |
| `Voice/Female/female_{hurt,downed}_NN.wav` | "Female Hurt Grunts & Groans", `female_hurt_grunts_groans_1.ogg`, https://opengameart.org/content/female-hurt-grunts-groans | Nocturnal_Vanguard (AuraVoice) | CC0 |
| `Voice/breathing_downed_loop.wav` | "Breathing Tired", `breathing tired.wav`, https://opengameart.org/content/breathing-tired (crossfaded into a loop) | mikeask | CC0 |
| `Footsteps/step_concrete_NN.wav`, `Footsteps/land_NN.wav` | Kenney "Impact Sounds" 1.0 (`footstep_concrete_000..004`, `impactSoft_heavy_000..002`), https://kenney.nl/assets/impact-sounds | Kenney (www.kenney.nl) | CC0 |

The four player prefabs now use character-specific Death/Hurt efforts in `Voice/Brimstone`,
`Voice/Clove`, `Voice/Gekko`, and `Voice/Sage`. These 17 mono 48 kHz WAVs were copied unchanged
from the user-approved `CharacterDeathHurt-2026-11-16` FModel export (VALORANT, Riot Games),
not from the CC0 sources above. The generic male/female sets remain available as fallbacks.
Hurt pools Light/Medium/Heavy takes; Downed reuses HurtHeavy because this export has no
dedicated Downed effort. Sage Death uses the revised 0.2824 s take. No spoken voicelines,
FallDmg, body-fall layer or ability audio is included.
