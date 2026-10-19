# PSX Pipes & Cables Texture Sample v1.0

[English](#english) · [繁體中文](#繁體中文) · [日本語](#日本語)

---

## English

### PSX Pipes & Cables Texture Sample v1.0

**PSX Pipes & Cables Texture Sample** is a small, free sample of retro, PSX-style tileable textures for industrial spaces: pipes, cables, brushed metal, rusted metal and grating.

All textures are generated procedurally by code (not generative-AI images). The generator code and this text were written with AI assistance.

### Contents

- **18 seamless textures**, 128 × 128 px. Each one has 4 maps:
  - `*_albedo.png` – unlit base colour (no baked light direction). Limited palette (≤ 32 colours from a 15-bit palette) with a 4×4 ordered dither.
  - `*_normal_gl.png` – normal map, **OpenGL convention (+Y / green up)**. Use this one for Godot, Blender and Unity.
  - `*_normal_dx.png` – the same normal map with the green channel inverted (**DirectX convention**, e.g. Unreal).
  - `*_roughness.png` – linear greyscale, white = rough, 16 levels.
  - Every texture gets a shared weathering pass (`tools/weather.py`): uneven dirt, irregular stains and splotches, drip streaks, edge wear, scratches, dirt in recesses and per-area tone variation. It is built from periodic noise, so it stays seamless and reproducible.
- **8 decals**, 128 × 128 RGBA, in `decals/`.
  - Straight (not premultiplied) alpha with 8 alpha levels.
  - The nearest opaque colour is copied into the transparent texels, so filtering and mipmaps do not create dark or white halos.
  - The alpha is 0 on the border.
- **Godot 4.3 demo** in `godot_demo/`: a corridor scene and a swatch gallery. Materials use all three maps with the Nearest texture filter.
- **Previews** in `previews/`: `contact_sheet.png` and `tiling_2x2.png`.
- **Generator and test scripts** in `tools/` (Python 3 + numpy + scipy + Pillow).

There is no metallic map. The demo materials set a single metallic value per material instead.

| # | Texture | Notes |
|---|---|---|
| 01 | pipes_wall_steel | 3 pipes (galvanised, painted, copper) with brackets on concrete |
| 02 | pipe_copper_strip | alpha cut-out, 2 copper pipes, soldered couplings, verdigris |
| 03 | pipe_rusty_strip | alpha cut-out, big flanged pipe, chipped paint over rust |
| 04 | pipe_corrugated_strip | alpha cut-out, yellow conduit + black corrugated hose |
| 05 | pipes_painted_vertical | colour-coded vertical pipes, clamps, flow arrow |
| 06 | cables_bundle_strip | alpha cut-out, cable bundle with zip ties |
| 07 | cable_tray | perforated galvanised tray with cables |
| 08 | cable_braided_strip | alpha cut-out, braided sleeve + 2 printed power cables |
| 09 | metal_brushed_panels | brushed aluminium panels with screws |
| 10 | metal_brushed_dark | dark gunmetal, many scratches (props / weapons) |
| 11 | metal_diamond_plate | tread plate, worn tops, dirt in recesses |
| 12 | metal_rust_heavy | heavy flaking rust with pits |
| 13 | metal_painted_rust_blue | blue paint chipped to rust, riveted seam |
| 14 | metal_corrugated_rust | galvanised corrugated sheet, rust streaks |
| 15 | metal_rivet_plates | riveted green plates, rust bleeding from rivets |
| 16 | grate_floor_bars | alpha cut-out, bar grating |
| 17 | grate_mesh_expanded | alpha cut-out, expanded metal mesh |
| 18 | grate_vent_slats | louvred vent panel |

Decals: oil_stain, crack, grime, rust_leak, hazard_stripes, puddle, scorch, debris.

#### Strip textures

The "strip" textures (02, 03, 04, 06, 08) and the grates 16 and 17 have binary alpha (cut-out).
- They tile in both directions.
- Put them on a quad in front of a wall, or on a cylinder.
- Use alpha scissor / alpha test (the demo uses a threshold of 0.5).

### Recommended import settings

- Texture filter: **Nearest**. Use no or lossless compression; VRAM compression smears the dithering.
- Albedo is sRGB. Normal and roughness maps are linear data.
- Godot: the `.import` files in `godot_demo/` already set Lossless, no mipmaps and no 3D VRAM detection. The materials use `texture_filter = Nearest`.

### Godot demo

1. Open `godot_demo/project.godot` in Godot 4.3. It uses the Compatibility (OpenGL) renderer.
2. The scenes are `scenes/corridor.tscn` (main scene) and `scenes/gallery.tscn`.
3. Decals in the demo are alpha-blended quads, because the `Decal` node is not available in the Compatibility renderer. Using the PNGs with `Decal` nodes in Forward+ has **not been tested**.
4. Screenshots (used for the store images):
   ```
   godot --path godot_demo res://scenes/corridor.tscn -- --cam=CamWide --shot=/absolute/path/shot.png
   ```
   - Corridor cameras: CamWide, CamWall, CamPit, CamRight, CamLeftWall, CamRightWall.
   - Gallery cameras: CamGallery, CamClose.
   - Use an absolute output path.

### What was tested

Tested **only on Linux** (Xvfb virtual display, Mesa llvmpipe, OpenGL 4.5) with **Godot 4.3 stable**:
- Headless `--import` gives 0 errors on both the first and the second run.
- Both scenes load and render. The screenshots in `previews/` and on the store page are real renders from this setup.

**Not tested:** Windows, macOS, real GPUs, Godot's Forward+ and Mobile renderers, other Godot versions, and other engines.

### Regenerating and checks

```
python3 tools/gen_textures.py --out .        # regenerate textures/ and decals/ (seed 20261018)
python3 tools/verify_reproducible.py         # regenerate to a temp dir and byte-compare
python3 tools/seam_check.py --self-test --sets textures
python3 tools/periodicity_test.py --inject
python3 tools/make_sheets.py                 # previews
python3 tools/make_godot_demo.py             # rebuild godot_demo/ (then re-import in Godot)
```

- **Reproducibility:** regeneration was byte-identical (80/80 files) with Python 3.13.5, numpy 2.5.2, scipy 1.18.1 and Pillow 12.3.0. Other library versions may round differently.
- **`seam_check.py`:** compares the wrap-around edge with every interior row and column pair of the albedo, normal and roughness maps together. It fails on obvious seams such as cropping, a lighting gradient or non-periodic noise; its self-test proves this with negative controls. It is a statistical heuristic and can miss very small, local mismatches. Current result: 17/18 sets PASS, 1 FAIL on `metal_brushed_dark` (vertical axis, ratio 1.12 against a limit of 1.10). The cause is a horizontal scratch on the first row, which makes the wrap step in average row brightness 5/255 against 4.8/255 for the largest interior step. The periodicity test below shows that this texture is exactly periodic, and the 2 × 2 tiling preview shows no visible seam. We count this as a false positive of the heuristic, but the FAIL is left in the report rather than tuned away.
- **`periodicity_test.py`:** regenerates everything with the coordinate grid shifted by whole tiles. A correctly tileable generator must give identical pixels. All 72 maps are identical. The `--inject` option proves the test catches a pattern whose period does not divide 128.

Preview labels use Fusion Pixel Font (SIL OFL 1.1). No font files are included.

### License

See `LICENSE.md`. In short:
- **Allowed:** commercial use, including adult works, and modification.
- **Not allowed:** reselling or redistributing the textures, decals or scripts by themselves.

---

## 繁體中文

### PSX Pipes & Cables Texture Sample v1.0（PSX 管線與電纜貼圖試用包）

**PSX Pipes & Cables Texture Sample** 是復古 PSX 風格、可無縫拼接的工業場景貼圖免費試用包：管線、電纜、髮絲紋金屬、鏽蝕金屬、格柵。

貼圖完全由程式程序化產生（非生成式 AI 生圖），產生器程式碼與文字為 AI 輔助。

### 內容

- **18 張無縫貼圖**，128 × 128 px。每張有 4 種貼圖：
  - `*_albedo.png`：無光照底色，不含烘焙的光線方向。限制色盤（15-bit 色盤中最多 32 色），並加上 4×4 有序抖色。
  - `*_normal_gl.png`：法線貼圖，**OpenGL 慣例（+Y／綠色朝上）**，Godot、Blender、Unity 用這張。
  - `*_normal_dx.png`：綠色通道反轉的同一張法線（**DirectX 慣例**，例如 Unreal）。
  - `*_roughness.png`：線性灰階，白 = 粗糙，16 階。
  - 所有貼圖都經過共用的風化處理（`tools/weather.py`）：不均勻髒污、不規則污漬與斑塊、垂流痕、邊緣磨損、刮痕、凹處積垢，以及區塊間的色調變化。以週期性雜訊產生，因此仍然無縫且可重現。
- **8 張貼花（decal）**，128 × 128 RGBA，放在 `decals/`。
  - 直通（非預乘）alpha，8 階。
  - 透明像素已填入最近的不透明顏色，濾波與 mipmap 不會產生黑邊或白邊。
  - 邊框 alpha 為 0。
- **Godot 4.3 示範**，放在 `godot_demo/`：走廊場景與材質展示牆。材質使用全部三種貼圖，濾波為 Nearest。
- **預覽圖**，放在 `previews/`：`contact_sheet.png`、`tiling_2x2.png`。
- **產生器與測試程式**，放在 `tools/`（Python 3 + numpy + scipy + Pillow）。

沒有 metallic 貼圖，示範材質改為每個材質設定單一 metallic 數值。貼圖清單見上方英文表格。

#### 條狀貼圖

條狀貼圖（02、03、04、06、08）與格柵 16、17 為二值 alpha（鏤空）。
- 兩個方向都可拼接。
- 可貼在牆前的四邊形或圓柱上。
- 請使用 alpha scissor／alpha test（示範專案的閾值為 0.5）。

### 建議匯入設定

- 濾波用 **Nearest**。不壓縮或用無損壓縮；VRAM 壓縮會糊掉抖色。
- Albedo 為 sRGB；法線與粗糙度為線性資料。
- Godot：`godot_demo/` 內的 `.import` 已設定 Lossless、無 mipmap、關閉 3D VRAM 偵測。材質使用 `texture_filter = Nearest`。

### Godot 示範

1. 用 Godot 4.3 開啟 `godot_demo/project.godot`，使用 Compatibility（OpenGL）渲染器。
2. 場景為 `scenes/corridor.tscn`（主場景）與 `scenes/gallery.tscn`。
3. 示範中的貼花是 alpha 混合的四邊形，因為 Compatibility 渲染器不支援 `Decal` 節點。在 Forward+ 用 `Decal` 節點搭配這些 PNG **未經測試**。
4. 截圖指令請見英文段落，輸出路徑請用絕對路徑。

### 測試範圍

**僅在 Linux**（Xvfb 虛擬顯示、Mesa llvmpipe、OpenGL 4.5）搭配 **Godot 4.3 stable** 測試：
- headless `--import` 第一次與第二次執行皆為 0 錯誤。
- 兩個場景皆能載入並渲染。`previews/` 與商店頁的截圖都是在此環境的實際渲染。

**未測試：** Windows、macOS、實體 GPU、Godot 的 Forward+ 與 Mobile 渲染器、其他 Godot 版本、其他引擎。

### 重新產生與檢查

指令請見英文段落。
- **可重現性：** 以 Python 3.13.5、numpy 2.5.2、scipy 1.18.1、Pillow 12.3.0 重新產生，結果逐位元組相同（80/80）。其他版本可能因捨入而不同。
- **`seam_check.py`：** 統計式接縫檢查。能抓到裁切、光照漸層、非週期雜訊等明顯接縫，自我測試中的負對照可證明；但可能漏掉很小的局部錯位。目前結果：18 組中 17 組 PASS，`metal_brushed_dark` 1 組 FAIL（垂直方向，比值 1.12，門檻 1.10）。原因是第一列有一道水平刮痕，使接縫處的列平均亮度差為 5/255，內部最大為 4.8/255。下方的週期性測試證明此貼圖完全週期，2 × 2 拼接預覽也看不到接縫；我們判斷為啟發式的誤報，但不為了通過而調整，保留在報告中。
- **`periodicity_test.py`：** 把座標平移整數個貼圖後重新產生。正確可拼接的產生器結果必須相同，目前 72/72 張相同。`--inject` 可證明它能抓到週期不整除 128 的圖樣。

### 授權

見 `LICENSE.md`。簡言之：
- **可以：** 商用（含成人作品）、修改。
- **不可以：** 單獨轉售或再散佈貼圖、貼花或程式本身。

---

## 日本語

### PSX Pipes & Cables Texture Sample v1.0（PSX 配管＆ケーブル テクスチャ サンプル）

**PSX Pipes & Cables Texture Sample** は、レトロな PSX 風のシームレスな工業系テクスチャの無料サンプルです：配管、ケーブル、ヘアライン金属、錆びた金属、グレーチング。

テクスチャはすべてプログラムによる手続き生成です（生成AIによる画像ではありません）。生成プログラムのコードと文章はAIの支援を受けて作成しています。

### 内容

- **シームレステクスチャ 18 種**、128 × 128 px。各テクスチャに 4 つのマップがあります：
  - `*_albedo.png`：ライティングなしのベースカラー（光の方向は焼き込んでいません）。15-bit パレットから最大 32 色に減色し、4×4 の組織的ディザをかけています。
  - `*_normal_gl.png`：法線マップ、**OpenGL 方式（+Y／緑が上）**。Godot・Blender・Unity ではこちらを使います。
  - `*_normal_dx.png`：緑チャンネルを反転した同じ法線マップ（**DirectX 方式**、例：Unreal）。
  - `*_roughness.png`：リニアのグレースケール、白 = 粗い、16 階調。
  - すべてのテクスチャに共通のウェザリング処理（`tools/weather.py`）をかけています：ムラのある汚れ、不規則なシミや斑点、垂れ跡、エッジの摩耗、引っかき傷、凹部の汚れ、領域ごとの色調の変化。周期的なノイズで作っているので、シームレスかつ再現可能です。
- **デカール 8 種**、128 × 128 RGBA、`decals/` にあります。
  - ストレート（非乗算済み）アルファ、8 階調。
  - 透明部分には最も近い不透明色を埋めてあるので、フィルタリングやミップマップで黒や白のフチが出ません。
  - 外周のアルファは 0 です。
- **Godot 4.3 デモ**、`godot_demo/` にあります：通路シーンとスウォッチギャラリー。マテリアルは 3 つのマップをすべて使い、フィルタは Nearest です。
- **プレビュー**、`previews/` にあります：`contact_sheet.png`、`tiling_2x2.png`。
- **生成・テスト用スクリプト**、`tools/` にあります（Python 3 + numpy + scipy + Pillow）。

メタリックマップはありません。デモのマテリアルではマテリアルごとに単一のメタリック値を設定しています。テクスチャ一覧は英語セクションの表を参照してください。

#### ストリップテクスチャ

ストリップ系（02、03、04、06、08）とグレーチング 16、17 は 2 値アルファ（切り抜き）です。
- 縦横どちらにもタイルできます。
- 壁の手前に置いたクアッドや円柱に貼って使います。
- アルファシザー／アルファテストを使ってください（デモのしきい値は 0.5）。

### 推奨インポート設定

- フィルタは **Nearest**。無圧縮またはロスレスにしてください。VRAM 圧縮ではディザが潰れます。
- アルベドは sRGB、法線とラフネスはリニアのデータです。
- Godot：`godot_demo/` の `.import` は Lossless、ミップマップなし、3D の VRAM 検出オフに設定済みです。マテリアルは `texture_filter = Nearest` です。

### Godot デモ

1. Godot 4.3 で `godot_demo/project.godot` を開きます。Compatibility（OpenGL）レンダラーを使います。
2. シーンは `scenes/corridor.tscn`（メイン）と `scenes/gallery.tscn` です。
3. デモのデカールはアルファブレンドのクアッドです。Compatibility レンダラーでは `Decal` ノードが使えないためです。Forward+ で `Decal` ノードと組み合わせる使い方は**未テスト**です。
4. スクリーンショットのコマンドは英語セクションを参照してください。出力先は絶対パスで指定します。

### テスト範囲

**Linux のみ**（Xvfb 仮想ディスプレイ、Mesa llvmpipe、OpenGL 4.5）、**Godot 4.3 stable** でテストしました：
- ヘッドレス `--import` は 1 回目・2 回目ともエラー 0 件でした。
- 両シーンとも読み込み・描画できました。`previews/` とストアの画像はこの環境での実際のレンダーです。

**未テスト：** Windows、macOS、実機 GPU、Godot の Forward+ と Mobile レンダラー、他の Godot バージョン、他のエンジン。

### 再生成とチェック

コマンドは英語セクションを参照してください。
- **再現性：** Python 3.13.5、numpy 2.5.2、scipy 1.18.1、Pillow 12.3.0 で再生成した結果はバイト単位で一致しました（80/80）。他のバージョンでは丸め誤差で変わる可能性があります。
- **`seam_check.py`：** 統計的なつなぎ目チェックです。クロップ、ライティングの勾配、非周期ノイズなどの明らかなつなぎ目は検出でき、セルフテストのネガティブコントロールで確認できます。ただしごく小さい局所的なずれは見逃すことがあります。現在の結果：18 セット中 17 が PASS、`metal_brushed_dark` の 1 セットが FAIL（縦方向、比 1.12、しきい値 1.10）。原因は 1 行目にある横方向の引っかき傷で、つなぎ目での行平均の明るさの差が 5/255（内部の最大は 4.8/255）になっています。下の周期性テストでこのテクスチャが厳密に周期的であることを確認しており、2 × 2 のタイルプレビューでもつなぎ目は見えません。ヒューリスティックの誤検出と判断していますが、通すための調整はせず、結果はそのまま残しています。
- **`periodicity_test.py`：** 座標をタイル単位でずらして再生成します。正しくタイルする生成器なら結果は同一になるはずで、現在 72/72 マップが同一です。`--inject` で、周期が 128 を割り切らない模様を検出できることを確認できます。

### ライセンス

`LICENSE.md` を参照してください。要約：
- **できること：** 商用利用（成人向け作品を含む）、改変。
- **できないこと：** テクスチャ・デカール・スクリプトそのものの再販・再配布。
