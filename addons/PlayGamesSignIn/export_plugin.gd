@tool
extends EditorPlugin

var export_plugin: PlayGamesExportPlugin


func _enter_tree():
	export_plugin = PlayGamesExportPlugin.new()
	add_export_plugin(export_plugin)


func _exit_tree():
	remove_export_plugin(export_plugin)
	export_plugin = null


class PlayGamesExportPlugin extends EditorExportPlugin:
	const NAME := "PlayGamesSignIn"
	const PROJECT_ID_SETTING := "crownroad/play_games/project_id"

	func _supports_platform(platform):
		return platform is EditorExportPlatformAndroid

	func _library(debug: bool) -> String:
		var variant := "debug" if debug else "release"
		return "%s/bin/%s/%s-%s.aar" % [NAME, variant, NAME, variant]

	# The AARs are build outputs (run native/PlayGamesSignIn/build.sh); without them the
	# export still succeeds and the game offers its other sign-in options.
	func _ready_for(debug: bool) -> bool:
		if FileAccess.file_exists("res://addons/" + _library(debug)):
			return true
		push_warning("Play Games sign-in left out: run native/PlayGamesSignIn/build.sh to build " + _library(debug))
		return false

	func _get_android_libraries(platform, debug):
		return PackedStringArray([_library(debug)]) if _ready_for(debug) else PackedStringArray()

	func _get_android_dependencies(platform, debug):
		# Keep in step with playGamesVersion in native/PlayGamesSignIn/build.gradle.
		return PackedStringArray(["com.google.android.gms:play-services-games-v2:21.0.0"]) if _ready_for(debug) else PackedStringArray()

	func _get_android_manifest_application_element_contents(platform, debug):
		var project_id := str(ProjectSettings.get_setting(PROJECT_ID_SETTING, "")).strip_edges()
		if project_id.is_empty() or not project_id.is_valid_int():
			return ""
		# "\u003" plus the ID's first digit spells that digit as an escape, so Android reads the
		# value as text instead of a number that would lose precision.
		return '<meta-data android:name="com.google.android.gms.games.APP_ID" android:value="\\u003%s" />\n' % project_id

	func _get_name():
		return NAME
