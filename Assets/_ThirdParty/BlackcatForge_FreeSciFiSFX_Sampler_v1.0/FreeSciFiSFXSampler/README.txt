FREE SCI-FI SFX SAMPLER - 20 sounds from 4 Blackcat Forge packs (v1.0)
=========================================================================

20 sounds, 5 from each of these packs, copied unchanged from the full packs:
  - Deep Space Ambience & Drones
  - Droid Voices
  - Sci-Fi Weapons & Impacts
  - Starship Engines & Thrusters

Every sound comes as WAV (48 kHz / 24-bit) and OGG Vorbis (same length, same sound).
Full list with lengths and channels: SOUND_LIST.csv

WHAT'S INSIDE
  Deep Space Ambience & Drones  (WAV/DeepSpaceAmbience/, OGG/DeepSpaceAmbience/)
    DSA_Derelict_HullCreaks_Loop       seamless 72 s loop: a dead ship groaning and creaking around you
    DSA_Stinger_SignalDetected         stinger: an unknown signal comes in
    DSA_Stinger_AlienPresence          stinger: something else is aboard
    DSA_Stinger_DistantExplosion       stinger: a far-off blast rolls through the hull
    DSA_Stinger_WarpArrival            stinger: dropping out of warp

  Droid Voices  (WAV/DroidVoices/, OGG/DroidVoices/)
    DV_Pip_Curious_01                  Pip (cute and chirpy): curious
    DV_Bloop_Happy_01                  Bloop (cute and chirpy): happy
    DV_Astra_Curious_01                Astra (helper): curious
    DV_Tonka_Angry_01                  Tonka (gruff and heavy): angry
    DV_Sentinel_Alarmed_02             Sentinel (menacing): alarmed

  Sci-Fi Weapons & Impacts  (WAV/SciFiWeaponsImpacts/, OGG/SciFiWeaponsImpacts/)
    SWI_Blaster_Pistol_01              blaster pistol shot
    SWI_Blaster_Heavy_01               heavy blaster shot
    SWI_Plasma_Shot_01                 plasma shot
    SWI_Impact_Blaster_01              blaster bolt impact
    SWI_Explosion_Small_01             small explosion

  Starship Engines & Thrusters  (WAV/StarshipEngines/, OGG/StarshipEngines/)
    SE_Fighter_Cruise_Loop             seamless 10 s loop: fighter engine at cruise
    SE_Fighter_Start                   fighter engine start-up (ends at idle level)
    SE_Racer_Flyby_Cruise              racer flyby with real Doppler shift
    SE_RCS_Burst_01                    RCS thruster burst (mono, for 3D)
    SE_Warp_In_01                      warp in


LOOPS
  DSA_Derelict_HullCreaks_Loop and SE_Fighter_Cruise_Loop loop seamlessly: the last sample flows into
  the first with no crossfade and no click. Turn looping on in your engine and play them as they are
  (Godot: import > Loop; Unity: Loop on the AudioSource; FMOD/Wwise: loop the region).
  Mono files (droids, weapons, the RCS burst) are mono on purpose, for positional 3D playback.

LEVELS
  True peak at or below -1 dBTP on every file. Engine loops are mastered so idle / cruise / boost
  sit at -21 / -18.5 / -16 LUFS (the cruise loop is here); ambience loops at -17 LUFS.

GODOT BLEND SCRIPT (not included)
  The full Starship Engines & Thrusters pack includes EngineBlender.gd for Godot 4. Set one throttle
  value from 0 to 1 and it crossfades each ship's Idle / Cruise / Boost loops, pitch-blends them, and
  plays the Start, Stop and boost overlays. It needs all three loops of a ship, so it's in the full pack.
  The full Droid Voices pack also has a Godot 4 typewriter-blip script.

GET THE FULL PACKS
  Deep Space Ambience & Drones: https://bllackcat1.itch.io/deep-space-ambience-drones
  Droid Voices: https://bllackcat1.itch.io/droid-voices
  Sci-Fi Weapons & Impacts: https://bllackcat1.itch.io/scifi-weapons-impacts
  Starship Engines & Thrusters: https://bllackcat1.itch.io/starship-engines-thrusters

  Black Friday sale: https://itch.io/s/206809
  All 10 Blackcat Forge packs in one bundle (from Dec 15, 2026): https://itch.io/s/206810/blackcat-forge-sci-fi-mega-pack-all-10-packs

HOW IT WAS MADE
  Every sound is synthesized in code by Blackcat Forge: no recorded samples, no third-party sound
  libraries and no generative-AI audio. The synthesis code was written with AI coding assistance;
  the sounds are the output of that code, not of an AI audio model.

LICENSE
  Free for commercial and non-commercial games, apps and videos, no attribution required.
  No resale or redistribution of the files themselves. Full text in LICENSE.txt.

Blackcat Forge - https://bllackcat1.itch.io
