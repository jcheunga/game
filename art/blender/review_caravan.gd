extends SceneTree
## Capture the real battle renderer with an isolated save, before combat begins.
## Run explicitly with --script; this authoring folder is excluded from import.

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var isolated := false
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--save-suffix=blender-review-"):
			isolated = true
	if not isolated:
		push_error("Provide --save-suffix=blender-review-<unique-id> to protect the player's save.")
		quit(1)
		return
	await process_frame
	var state := root.get_node("GameState")
	state.call("SetAnalyticsConsent", false)
	state.call("SetShowHints", false)
	state.call("SetSelectedStage", 1)
	state.call("PrepareCampaignBattle")
	var texture := load("res://assets/structures/war_wagon.png") as Texture2D
	if texture == null or texture.get_size() != Vector2(1440, 1120):
		push_error("Expected the 1440x1120 authored caravan texture.")
		quit(1)
		return
	var scene := load("res://scenes/Battle.tscn") as PackedScene
	var battle := scene.instantiate()
	root.add_child(battle)
	battle.set_physics_process(false)
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var directory := ProjectSettings.globalize_path("res://artifacts/blender")
	DirAccess.make_dir_recursive_absolute(directory)
	var output := directory.path_join("caravan-in-battle.png")
	var result := root.get_texture().get_image().save_png(output)
	if result != OK:
		push_error("Could not write the caravan battle preview.")
		quit(1)
		return
	print("CARAVAN_PREVIEW_OK: ", output)
	battle.queue_free()
	await process_frame
	quit(0)
