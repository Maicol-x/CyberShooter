extends Node
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
