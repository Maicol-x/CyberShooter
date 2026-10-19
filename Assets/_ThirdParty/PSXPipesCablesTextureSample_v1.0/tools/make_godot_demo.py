#!/usr/bin/env python3
"""Builds the Godot 4.3 demo project (godot_demo/) from textures/ and decals/.

Writes: project.godot, .import presets (lossless, Nearest-friendly, no VRAM
compression), one StandardMaterial3D .tres per texture, the corridor scene,
the swatch gallery scene and the screenshot helper script.

    python3 tools/make_godot_demo.py
"""
import os, sys, shutil, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.abspath(os.path.join(HERE, ".."))
OUT = os.path.join(KIT, "godot_demo")
sys.path.insert(0, HERE)
from textures_def import TEXTURES
from decals_def import DECALS

TEX = [n for n, _ in TEXTURES]
DEC = [n for n, _ in DECALS]
ALPHA_TEX = {"pipe_copper_strip", "pipe_rusty_strip", "pipe_corrugated_strip", "cables_bundle_strip",
             "cable_braided_strip", "grate_floor_bars", "grate_mesh_expanded"}
METALLIC = {"metal_brushed_panels": 0.5, "metal_brushed_dark": 0.5,
            "metal_diamond_plate": 0.45, "pipe_copper_strip": 0.45, "cable_tray": 0.35,
            "grate_floor_bars": 0.3, "metal_corrugated_rust": 0.25}

IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{src}"

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/hdr_compression=1
compress/normal_map={nm}
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=0
"""

PROJECT = """; Engine configuration file.
config_version=5

[application]

config/name="PSX Pipes & Cables Texture Sample - Demo"
run/main_scene="res://scenes/corridor.tscn"
config/features=PackedStringArray("4.3", "GL Compatibility")

[display]

window/size/viewport_width=1280
window/size/viewport_height=720

[rendering]

renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"
textures/canvas_textures/default_texture_filter=0
anti_aliasing/quality/msaa_3d=0
"""

SHOT_GD = """extends Node
## Screenshot helper used for the store images and for automated testing.
## godot --path godot_demo res://scenes/corridor.tscn -- --cam=CamWide --shot=/abs/path.png
## Without --shot the scene simply runs (Camera "cam" argument still applies).

func _ready() -> void:
	var out := ""
	var cam := ""
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--shot="):
			out = a.substr(7)
		elif a.begins_with("--cam="):
			cam = a.substr(6)
	if cam != "":
		var c := get_parent().get_node_or_null(NodePath("Cameras/" + cam)) as Camera3D
		if c == null:
			push_error("camera not found: " + cam)
		else:
			c.current = true
	if out == "":
		return
	for i in 20:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var err := img.save_png(out)
	if err != OK:
		push_error("save_png failed: %s" % err)
	print("SHOT_SAVED ", out, " ", img.get_width(), "x", img.get_height())
	get_tree().quit()
"""


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="\n") as f:
        f.write(text)


# ------------------------------------------------------------------ transforms
def basis(rx=0.0, ry=0.0, rz=0.0):
    """Godot default Euler order YXZ (degrees). Returns 3x3 matrix."""
    rx, ry, rz = (math.radians(v) for v in (rx, ry, rz))
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    X = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Y = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Z = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Y @ X @ Z


def tf(pos, rot=(0, 0, 0), scale=1.0):
    b = basis(*rot) * scale
    vals = list(b.reshape(-1)) + list(pos)
    return "Transform3D(" + ", ".join(_f(v) for v in vals) + ")"


def _f(v):
    v = 0.0 if abs(v) < 1e-9 else float(v)
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


# ------------------------------------------------------------------ scene builder
class Scene:
    def __init__(self, root, root_type="Node3D"):
        self.ext, self.sub, self.nodes = [], [], []
        self.ext_ids, self.sub_ids = {}, {}
        self.nodes.append(f'[node name="{root}" type="{root_type}"]\n')

    def ext_res(self, typ, path):
        key = (typ, path)
        if key not in self.ext_ids:
            i = f"{len(self.ext_ids) + 1}_r"
            self.ext_ids[key] = i
            self.ext.append(f'[ext_resource type="{typ}" path="{path}" id="{i}"]\n')
        return f'ExtResource("{self.ext_ids[key]}")'

    def sub_res(self, typ, props, key=None):
        key = key or (typ, tuple(sorted(props.items())))
        if key not in self.sub_ids:
            i = f"{typ}_{len(self.sub_ids) + 1}"
            self.sub_ids[key] = i
            body = "".join(f"{k} = {v}\n" for k, v in props.items())
            self.sub.append(f'[sub_resource type="{typ}" id="{i}"]\n{body}')
        return f'SubResource("{self.sub_ids[key]}")'

    def node(self, name, typ, parent=".", **props):
        body = "".join(f"{k} = {v}\n" for k, v in props.items())
        self.nodes.append(f'[node name="{name}" type="{typ}" parent="{parent}"]\n{body}')

    def text(self):
        steps = len(self.ext) + len(self.sub) + 1
        return (f"[gd_scene load_steps={steps} format=3]\n\n" + "\n".join(self.ext) + "\n" +
                "\n".join(self.sub) + "\n" + "\n".join(self.nodes))


def mat_props(sc, name, uv=(1, 1), decal=False):
    if decal:
        p = {"transparency": "1", "albedo_texture": sc.ext_res("Texture2D", f"res://decals/{name}.png"),
             "roughness": "0.85" if name != "decal_puddle" else "0.1",
             "texture_filter": "0", "render_priority": "1"}
        return p
    p = {
        "albedo_texture": sc.ext_res("Texture2D", f"res://textures/{name}/{name}_albedo.png"),
        "metallic": _f(METALLIC.get(name, 0.0)),
        "roughness_texture": sc.ext_res("Texture2D", f"res://textures/{name}/{name}_roughness.png"),
        "normal_enabled": "true",
        "normal_texture": sc.ext_res("Texture2D", f"res://textures/{name}/{name}_normal_gl.png"),
        "uv1_scale": f"Vector3({_f(uv[0])}, {_f(uv[1])}, 1)",
        "texture_filter": "0",
    }
    if name in ALPHA_TEX:
        p = {"transparency": "2", "alpha_scissor_threshold": "0.5", "cull_mode": "2", **p}
    return p


def quad(sc, nm, parent, size, pos, rot, mat, uv=(1, 1), decal=False):
    mesh = sc.sub_res("QuadMesh", {"size": f"Vector2({_f(size[0])}, {_f(size[1])})"})
    if decal:
        m = sc.ext_res("Material", f"res://materials/decals/{mat}.tres")
    elif tuple(uv) == (1, 1):
        m = sc.ext_res("Material", f"res://materials/{mat}.tres")
    else:  # same maps, scene-local copy with the needed tiling
        m = sc.sub_res("StandardMaterial3D", mat_props(sc, mat, uv, decal))
    sc.node(nm, "MeshInstance3D", parent, transform=tf(pos, rot), mesh=mesh,
            **{"surface_material_override/0": m})


def env_res(sc, bg, ambient, energy, fog=0.0):
    props = {"background_mode": "1", "background_color": bg,
             "ambient_light_source": "2", "ambient_light_color": ambient,
             "ambient_light_energy": _f(energy), "tonemap_mode": "2"}
    if fog:
        props.update({"fog_enabled": "true", "fog_light_color": "Color(0.05, 0.055, 0.06, 1)",
                      "fog_density": _f(fog)})
    return sc.sub_res("Environment", props)


# ------------------------------------------------------------------ corridor
def corridor():
    sc = Scene("Corridor")
    sc.node("WorldEnvironment", "WorldEnvironment",
            environment=env_res(sc, "Color(0.02, 0.022, 0.026, 1)", "Color(0.55, 0.6, 0.7, 1)", 0.35, fog=0.035))
    sc.node("Geometry", "Node3D")
    G = "Geometry"
    L = 24.0
    # floor: plate | grate over pit | plate
    quad(sc, "FloorA", G, (4, 8), (0, 0, -4), (-90, 0, 0), "metal_diamond_plate", (4, 8))
    quad(sc, "FloorGrate", G, (4, 4), (0, 0, -10), (-90, 0, 0), "grate_floor_bars", (4, 4))
    quad(sc, "FloorC", G, (4, 12), (0, 0, -18), (-90, 0, 0), "metal_diamond_plate", (4, 12))
    quad(sc, "PitFloor", G, (4, 4), (0, -1.2, -10), (-90, 0, 0), "metal_rust_heavy", (4, 4))
    quad(sc, "PitWallN", G, (4, 1.2), (0, -0.6, -8), (0, 180, 0), "metal_rivet_plates", (4, 1.2))
    quad(sc, "PitWallS", G, (4, 1.2), (0, -0.6, -12), (0, 0, 0), "metal_rivet_plates", (4, 1.2))
    quad(sc, "PitWallW", G, (4, 1.2), (-2, -0.6, -10), (0, 90, 0), "metal_rivet_plates", (4, 1.2))
    quad(sc, "PitWallE", G, (4, 1.2), (2, -0.6, -10), (0, -90, 0), "metal_rivet_plates", (4, 1.2))
    quad(sc, "PitPipe", G, (4, 1), (0, -0.75, -11.6), (0, 0, 0), "pipe_rusty_strip", (4, 1))
    quad(sc, "PitCables", G, (4, 0.5), (0, -1.0, -8.2), (0, 180, 0), "cables_bundle_strip", (8, 1))
    # walls (4 m segments, 3 m high)
    left = ["metal_painted_rust_blue", "pipes_painted_vertical", "metal_corrugated_rust",
            "metal_rivet_plates", "metal_painted_rust_blue", "metal_rust_heavy"]
    right = ["pipes_wall_steel", "grate_vent_slats", "metal_corrugated_rust",
             "metal_brushed_panels", "pipes_wall_steel", "metal_brushed_dark"]
    for i, m in enumerate(left):
        quad(sc, f"WallL{i}", G, (4, 3), (-2, 1.5, -2 - 4 * i), (0, 90, 0), m, (4, 3))
    for i, m in enumerate(right):
        quad(sc, f"WallR{i}", G, (4, 3), (2, 1.5, -2 - 4 * i), (0, -90, 0), m, (4, 3))
    quad(sc, "EndWall", G, (4, 3), (0, 1.5, -L), (0, 0, 0), "metal_painted_rust_blue", (4, 3))
    quad(sc, "EndMesh", G, (4, 3), (0, 1.5, -L + 1.2), (0, 0, 0), "grate_mesh_expanded", (4, 3))
    quad(sc, "BackWall", G, (4, 3), (0, 1.5, 0), (0, 180, 0), "metal_rivet_plates", (4, 3))
    # ceiling + cable tray
    quad(sc, "Ceiling", G, (4, L), (0, 3, -L / 2), (90, 0, 0), "metal_brushed_dark", (4, L))
    quad(sc, "CableTray", G, (L, 1), (0.9, 2.95, -L / 2), (90, 0, 90), "cable_tray", (L, 1))
    # pipe / cable strips along the walls (alpha cut-outs, 0.1-0.2 m in front)
    quad(sc, "StripCopperL", G, (L, 0.5), (-1.88, 2.55, -L / 2), (0, 90, 0), "pipe_copper_strip", (L * 2, 1))
    quad(sc, "StripCorrL", G, (L, 0.6), (-1.85, 0.35, -L / 2), (0, 90, 0), "pipe_corrugated_strip", (L / 0.6, 1))
    quad(sc, "StripCablesR", G, (L, 0.5), (1.9, 2.6, -L / 2), (0, -90, 0), "cables_bundle_strip", (L * 2, 1))
    quad(sc, "StripBraidR", G, (L, 0.5), (1.88, 0.3, -L / 2), (0, -90, 0), "cable_braided_strip", (L * 2, 1))
    # decals as offset quads (Compatibility renderer has no Decal node support)
    sc.node("Decals", "Node3D")
    D = "Decals"
    quad(sc, "OilStain", D, (1.6, 1.6), (-0.4, 0.012, -3.5), (-90, 0, 0), "decal_oil_stain", decal=True)
    quad(sc, "Puddle", D, (2.0, 2.0), (0.6, 0.012, -15), (-90, 0, 0), "decal_puddle", decal=True)
    quad(sc, "Debris", D, (1.2, 1.2), (-1.2, 0.014, -6.5), (-90, 0, 0), "decal_debris", decal=True)
    quad(sc, "Scorch", D, (1.8, 1.8), (0.3, 0.013, -19.5), (-90, 0, 0), "decal_scorch", decal=True)
    quad(sc, "Hazard", D, (4, 1), (0, 0.015, -7.4), (-90, 0, 0), "decal_hazard_stripes", decal=True)
    quad(sc, "Hazard2", D, (4, 1), (0, 0.015, -12.6), (-90, 0, 0), "decal_hazard_stripes", decal=True)
    quad(sc, "RustLeak", D, (1.2, 1.2), (-1.985, 1.6, -1.6), (0, 90, 0), "decal_rust_leak", decal=True)
    quad(sc, "RustLeak2", D, (1.4, 1.4), (1.985, 1.3, -14.3), (0, -90, 0), "decal_rust_leak", decal=True)
    quad(sc, "Crack", D, (1.3, 1.3), (-1.985, 1.2, -10.5), (0, 90, 0), "decal_crack", decal=True)
    quad(sc, "Grime", D, (2.0, 2.0), (1.985, 0.8, -5.0), (0, -90, 0), "decal_grime", decal=True)
    quad(sc, "Grime2", D, (2.2, 2.2), (-1.985, 0.9, -17.0), (0, 90, 0), "decal_grime", decal=True)
    # lights + fixtures
    sc.node("Lights", "Node3D")
    lamp_mat = sc.sub_res("StandardMaterial3D", {"albedo_color": "Color(1, 0.9, 0.7, 1)",
                                                 "emission_enabled": "true",
                                                 "emission": "Color(1, 0.85, 0.6, 1)",
                                                 "emission_energy_multiplier": "2.0"})
    lamp_mesh = sc.sub_res("BoxMesh", {"size": "Vector3(0.6, 0.06, 0.25)"})
    cols = ["Color(1, 0.82, 0.6, 1)", "Color(0.75, 0.85, 1, 1)", "Color(1, 0.82, 0.6, 1)", "Color(1, 0.45, 0.3, 1)"]
    for i, z in enumerate((-3, -9, -15, -21)):
        sc.node(f"Lamp{i}", "OmniLight3D", "Lights", transform=tf((-0.6, 2.75, z)),
                light_color=cols[i], light_energy="1.3", omni_range="7.5", omni_attenuation="1.2",
                shadow_enabled="true" if i in (0, 1) else "false")
        sc.node(f"Fixture{i}", "MeshInstance3D", "Lights", transform=tf((-0.6, 2.95, z)),
                mesh=lamp_mesh, **{"surface_material_override/0": lamp_mat})
    sc.node("PitLight", "OmniLight3D", "Lights", transform=tf((0, -0.6, -10)),
            light_color="Color(0.4, 0.7, 1, 1)", light_energy="1.2", omni_range="3")
    # cameras
    sc.node("Cameras", "Node3D")
    sc.node("CamWide", "Camera3D", "Cameras", transform=tf((0.25, 1.6, -0.6), (-3, 4, 0)), fov="70", current="true")
    sc.node("CamWall", "Camera3D", "Cameras", transform=tf((-0.4, 1.55, -12.0), (-4, -62, 0)), fov="62")
    sc.node("CamPit", "Camera3D", "Cameras", transform=tf((0.0, 1.7, -6.0), (-42, 0, 0)), fov="65")
    sc.node("CamRight", "Camera3D", "Cameras", transform=tf((1.3, 1.45, -2.6), (-5, 50, 0)), fov="62")
    sc.node("CamLeftWall", "Camera3D", "Cameras", transform=tf((1.2, 1.5, -18.0), (-3, 90, 0)), fov="70")
    sc.node("CamRightWall", "Camera3D", "Cameras", transform=tf((-1.2, 1.5, -10.0), (-3, -90, 0)), fov="70")
    sc.node("Shot", "Node", script=sc.ext_res("Script", "res://scripts/shot.gd"))
    return sc.text()


# ------------------------------------------------------------------ gallery
def gallery():
    sc = Scene("Gallery")
    sc.node("WorldEnvironment", "WorldEnvironment",
            environment=env_res(sc, "Color(0.09, 0.095, 0.1, 1)", "Color(0.8, 0.82, 0.9, 1)", 0.55))
    sc.node("Sun", "DirectionalLight3D", transform=tf((0, 0, 0), (-38, -28, 0)), light_energy="1.1",
            light_color="Color(1, 0.96, 0.9, 1)")
    sc.node("Fill", "OmniLight3D", transform=tf((0, 0.5, 4.5)), light_energy="0.8", omni_range="12",
            light_color="Color(0.75, 0.85, 1, 1)")
    back = sc.sub_res("StandardMaterial3D", {"albedo_color": "Color(0.16, 0.165, 0.17, 1)", "roughness": "1"})
    bmesh = sc.sub_res("QuadMesh", {"size": "Vector2(20, 12)"})
    sc.node("Backdrop", "MeshInstance3D", transform=tf((0, 0, -0.08)), mesh=bmesh,
            **{"surface_material_override/0": back})
    sc.node("Swatches", "Node3D")
    sc.node("Labels", "Node3D")
    for i, n in enumerate(TEX):
        r, c = divmod(i, 5)
        x = (c - 2) * 2.1 - 1.6
        y = 2.45 - r * 1.62
        quad(sc, f"S_{n}", "Swatches", (1, 1), (x, y + 0.08, 0), (0, 0, 0), n, (1, 1))
        quad(sc, f"S2_{n}", "Swatches", (1, 1), (x + 1.0, y + 0.08, 0), (0, 0, 0), n, (1, 1))
        sc.node(f"L_{n}", "Label3D", "Labels", transform=tf((x + 0.5, y - 0.58, 0.01)),
                text=f'"{i + 1:02d} {n}"', pixel_size="0.005", font_size="26", outline_size="6",
                modulate="Color(0.9, 0.9, 0.85, 1)")
    plate = sc.sub_res("StandardMaterial3D", {"albedo_color": "Color(0.42, 0.43, 0.44, 1)", "roughness": "0.9"})
    pmesh = sc.sub_res("QuadMesh", {"size": "Vector2(1, 1)"})
    for i, n in enumerate(DEC):
        x = 5.5 + (i % 2) * 1.2
        y = 2.53 - (i // 2) * 1.62
        sc.node(f"P_{n}", "MeshInstance3D", "Swatches", transform=tf((x, y, 0)), mesh=pmesh,
                **{"surface_material_override/0": plate})
        quad(sc, f"D_{n}", "Swatches", (1, 1), (x, y, 0.004), (0, 0, 0), n, decal=True)
        sc.node(f"LD_{n}", "Label3D", "Labels", transform=tf((x, y - 0.6, 0.01)),
                text=f'"{n[6:]}"', pixel_size="0.005", font_size="24", outline_size="6",
                modulate="Color(0.9, 0.9, 0.85, 1)")
    sc.node("Cameras", "Node3D")
    sc.node("CamGallery", "Camera3D", "Cameras", transform=tf((0.55, 0.12, 9.0)), fov="47", current="true")
    sc.node("CamClose", "Camera3D", "Cameras", transform=tf((-3.2, 1.9, 2.4), (-6, -18, 0)), fov="50")
    sc.node("Shot", "Node", script=sc.ext_res("Script", "res://scripts/shot.gd"))
    return sc.text()


def material_tres(n, decal=False):
    sc = Scene("x")
    props = mat_props(sc, n, decal=decal)
    ext = "".join(sc.ext)
    body = "".join(f"{k} = {v}\n" for k, v in props.items())
    return f"[gd_resource type=\"StandardMaterial3D\" load_steps={len(sc.ext) + 1} format=3]\n\n{ext}\n[resource]\n{body}"


def main():
    if os.path.isdir(OUT):
        for sub in ("textures", "decals", "materials", "scenes", "scripts"):
            shutil.rmtree(os.path.join(OUT, sub), ignore_errors=True)
    os.makedirs(OUT, exist_ok=True)
    write(os.path.join(OUT, "project.godot"), PROJECT)
    for n in TEX:
        for k in ("albedo", "normal_gl", "roughness"):
            rel = f"textures/{n}/{n}_{k}.png"
            os.makedirs(os.path.dirname(os.path.join(OUT, rel)), exist_ok=True)
            shutil.copyfile(os.path.join(KIT, rel), os.path.join(OUT, rel))
            write(os.path.join(OUT, rel + ".import"), IMPORT.format(src="res://" + rel, nm=0))
        write(os.path.join(OUT, "materials", f"{n}.tres"), material_tres(n))
    for n in DEC:
        rel = f"decals/{n}.png"
        os.makedirs(os.path.join(OUT, "decals"), exist_ok=True)
        shutil.copyfile(os.path.join(KIT, rel), os.path.join(OUT, rel))
        write(os.path.join(OUT, rel + ".import"), IMPORT.format(src="res://" + rel, nm=0))
        write(os.path.join(OUT, "materials", "decals", f"{n}.tres"), material_tres(n, decal=True))
    write(os.path.join(OUT, "scripts", "shot.gd"), SHOT_GD)
    write(os.path.join(OUT, "scenes", "corridor.tscn"), corridor())
    write(os.path.join(OUT, "scenes", "gallery.tscn"), gallery())
    print("godot_demo written:", OUT)


if __name__ == "__main__":
    main()
