NULLSIGNAL // SURFACES — Vol.1: DECK & HULL
Dark-tech sci-fi PBR texture pack / ダークテックSF PBRテクスチャパック
=====================================================================

5 seamless materials x 3 resolutions (1K / 2K / 4K), full PBR map sets.
5種のシームレス素材 x 3解像度(1K/2K/4K)、フルPBRマップ構成。

MATERIALS / 素材
----------------
DECK01   Sci-fi floor deck plates + teal light strips (emissive)
         SFデッキパネル+ティール導光ストリップ(発光)
WALL01   Hull wall plating + amber hazard band + status LEDs (emissive)
         船殻壁パネル+アンバーハザード帯+状態LED(発光)
TREAD01  Classic treadplate (staggered +/-45 bars)
         縞鋼板(千鳥±45°)
GRATE01  Industrial floor grating + OPACITY map (real holes)
         床グレーチング+不透明度マップ(実際に抜ける穴)
TECH01   Dense greeble tech panel + LEDs (emissive)
         高密度テックパネル+LED(発光)

MAPS / マップ
-------------
*_BaseColor.png   sRGB albedo
*_Normal_GL.png   OpenGL normal (Y+)  -> Unity, Godot, Blender
*_Normal_DX.png   DirectX normal (Y-) -> Unreal
*_Roughness.png   linear
*_Metallic.png    linear
*_AO.png          ambient occlusion
*_Height.png      16-bit height/displacement
*_Emissive.png    emissive color (DECK01/WALL01/TECH01)
*_Opacity.png     cutout mask (GRATE01)

USAGE NOTES / 使い方メモ
-----------------------
- Unity (Standard/URP/HDRP): BaseColor->Albedo, Normal_GL->Normal,
  Metallic+Roughness: URP/HDRP use Smoothness = 1 - Roughness
  (invert Roughness or use a mask map).
- Unreal: use Normal_DX. Roughness/Metallic plug in directly.
- VRChat worlds: works as standard Unity materials. GRATE01 works well
  with cutout shaders.
- All materials tile seamlessly in both axes.
  全素材とも縦横シームレスにタイルします。

REVISION / 更新履歴
------------------
2026-09-16  TREAD01: Normal_GL / Normal_DX regenerated. The strength used for
            the original pair was too low for this material's shallow relief,
            so the bars barely registered in lighting. Every other map is
            unchanged, and the 16-bit Height map was the source, so a scene
            built on the earlier files will not shift -- only the shading gets
            its relief back.
            TREAD01の法線(Normal_GL/Normal_DX)を作り直しました。起伏の浅い素材に
            対して強さの設定が弱すぎたため、縞が陰影にほとんど出ていませんでした。
            他のマップは一切変わっていないので、既存のシーンの位置や見た目はずれません。


LICENSE: see LICENSE.txt (commercial OK / no redistribution / no AI training)
ライセンス: LICENSE.txt 参照(商用OK/再配布禁止/AI学習禁止)

More packs / 他のパック:
  itch:    https://nkyo-p.itch.io
  BOOTH:   https://nkyo-p.booth.pm
  Gumroad: https://suto3.gumroad.com

NULLSIGNAL — UI for machines that don't exist. Surfaces for ships that never launched.
