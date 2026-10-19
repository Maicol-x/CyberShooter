# HUD Elements: FREE Sample

A free taste of **HUD Elements**, a generic game HUD kit drawn as SVG (dark glass and light themes, four accent colours, 9-slice margins, PNG at 1x/2x/3x, a Godot 4 demo HUD).

**Get the full pack:** https://stationsupply.itch.io/hud-elements-svg-game-hud-kit

## What is in this sample (blue accent, dark and light)
- **Slot frame, 64 px:** all 6 states (normal, hover, pressed, disabled, focused, selected)
- **Cooldown ring, 96 px:** 0 (ready), 25, 50, 75 and 100 % time left
- **Health bar, 192 x 24:** under, frame and fill layers plus a 50 % picture
- **Crosshair:** circle with ticks in 3 states (normal, ready, hostile)
- **Key prompt:** letter key E (normal, pressed)
- **HUD panel**, 128 x 96, with its 9-slice margins in `nine-slice.json`

42 SVG files (21 per theme) and 126 PNG files (1x, 2x, 3x), a preview sheet per theme, the 9-slice table, and the license.

```
svg/dark|light/<category>/     SVG sources
png/1x|2x|3x/dark|light/...    PNG exports
nine-slice.json                margins (px at 1x) for the slot, the bar layers and the panel
preview/                       sample_dark.png, sample_light.png
```

## Using the files
- **Godot 4:** on a `StyleBoxTexture` enter the margins from `nine-slice.json` as **Texture Margins** (on a `NinePatchRect` they are called **Patch Margin**). Bars: stack the `_under`, `_fill` and `_frame` files in a `TextureProgressBar`.
- **Unity:** import the 2x PNGs as Sprite (2D and UI), Pixels Per Unit 2, enter the margins x2 as Border, Image Type Sliced.
- **Web:** use the SVG files directly, or CSS `border-image` with the PNGs.

The full pack adds: all four bar types in two sizes and vertical, boss bar, rings in three sizes, square cooldowns, buff and debuff frames, minimap frames and pips, 12 crosshairs, ammo counter, quest tracker, compass, damage direction indicators, objective markers, notification toasts, key prompt badges, unit frames, the recolour tool and the Godot 4 demo HUD.

## License
See LICENSE: use in your own games and apps, commercial too; no resale or redistribution of the files.

## AI disclosure
All graphics are hand-coded SVG vectors; no AI image generation was used. The SVG code was written with an AI assistant.
