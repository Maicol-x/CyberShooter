# IGNITION — Sci-Fi Startup VFX

Version v001 · by domontypef

One science-fiction startup effect, supplied in three color variants: Cyan, Amber and Violet. Rotating rings gather light into a core, followed by an ignition flash and an expanding ring. Useful for device activation, skill triggers, portals and interface transitions.

## Included

- 270 transparent RGBA PNG frames: 90 frames per color, 256 × 256 px.
- Three sprite sheets: 2560 × 2304 px, 10 columns × 9 rows.
- Three JSON files with frame paths, pixel rectangles, timing and a centered anchor.
- An offline browser preview using the supplied sprite sheets. Extract the ZIP and open `preview/index.html`.
- The effect's AI-assisted JavaScript source in `source/effect.mjs`, creation notes and license.

## Playback and import

Play frames 000–089 at 30 fps for a 3-second one-shot animation. The first and last frames are transparent. The browser's Repeat option retriggers the startup every 3 seconds; it does not turn the effect into a seamless continuous loop. No sound is included.

The files use **straight alpha** and normal/source-over blending. Start with normal alpha blending; additive blending will change their appearance. JSON anchor `[0.5, 0.5]` means the center of each 256 px frame. Use `(id, palette)` to identify a variant because the three JSON files share one effect id.

For a sprite sheet, retain its original 2560 × 2304 dimensions, split it into a 10 × 9 grid, and preserve all 90 frames. An importer capped at 2048 px will rescale this sheet and invalidate the original JSON rectangles. Raise the texture size limit, or import the individual frames instead. Disable automatic trimming if you need the fixed center and frame size. There is no cell padding; avoid mipmaps or filtering across neighboring cells. Test your engine's sampling at the intended display size.

For an individual sequence, read `assets/<color>/effect.json`; its `sheet` and `frames[].file` paths are relative to that JSON file. JSON timing uses milliseconds, with each frame lasting approximately 33.333 ms.

## Scope and provenance

The PNGs are pre-rendered from an original deterministic Canvas 2D effect, developed with AI assistance. No third-party artwork, font, model or sound is included. An AI-generated composition reference helped plan the development UI; that reference image is excluded from this pack. Documentation and demo code were also prepared with AI assistance.

`source/effect.mjs` is provided for inspection and modification. It is not a complete regeneration toolkit. The supplied PNGs are the reference assets; another Canvas implementation may rasterize glow or anti-aliasing differently. The offline preview only reads those PNGs and does not regenerate them.

Browser layout and controls were checked on desktop and a simulated narrow viewport. Unity, Godot, Unreal and physical phones have not been tested. There are no engine prefabs or plugins in this version.

Commercial and personal use, modification and recoloring are permitted under `LICENSE.md`. Credit is optional. Redistributing or reselling the reusable assets is prohibited. Read the license for the complete terms.

---

## 日本語

SF機器の起動をイメージした**演出1種類・色違い3種**です。各色256 × 256 px、30fps、90枚、3秒。透過PNG連番270枚、10列 × 9行のシート3枚、JSON3個、ブラウザ確認用デモとJavaScriptソースを収録しています。音声、ゲームエンジン専用設定、シームレスな連続ループは含みません。

ZIPを展開して `preview/index.html` を開くと、配布シートを再生できます。Repeatは3秒ごとの再発動です。画像はストレートアルファ・通常合成を想定しています。シートは2560 × 2304 pxなので、2048 px制限による自動縮小を避け、元サイズと90フレームを保持してください。自動トリミング、セル境界のフィルタリング、mipmapは取り込み先で確認してください。

AI支援で開発したコードからPNGを生成しています。構成検討用のAI画像は未収録です。ブラウザ確認済みですが、実機スマートフォンやゲームエンジンへの取り込みは未検証です。ソースは変更・閲覧用で、完全な再生成環境は含みません。

商用・非商用利用、改変・色変更が可能です。クレジットは任意です。再利用可能な素材としての再配布・再販売は禁止です。詳しくは `LICENSE.md` を確認してください。
