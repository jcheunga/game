@tool
extends EditorPlugin

var export_plugin: CrownroadAdsExportPlugin


func _enter_tree():
	export_plugin = CrownroadAdsExportPlugin.new()
	add_export_plugin(export_plugin)


func _exit_tree():
	remove_export_plugin(export_plugin)
	export_plugin = null


class CrownroadAdsExportPlugin extends EditorExportPlugin:
	const NAME := "CrownroadAds"
	const APP_ID_SETTING := "crownroad/ads/android_app_id"
	const UNIT_SETTING := "crownroad/ads/android_rewarded_unit"
	# Google's published test app, for debug builds without a real one.
	const TEST_APP_ID := "ca-app-pub-3940256099942544~3347511713"

	func _supports_platform(platform):
		return platform is EditorExportPlatformAndroid

	func _setting(name: String) -> String:
		return str(ProjectSettings.get_setting(name, "")).strip_edges()

	func _library(debug: bool) -> String:
		var variant := "debug" if debug else "release"
		return "%s/bin/%s/%s-%s.aar" % [NAME, variant, NAME, variant]

	# Debug builds show Google's test ads. A release build carries the ad SDK only with the real AdMob app and ad unit
	# IDs set, so a build without them has no ads, no advertising ID permission and nothing to declare. The AARs are
	# build outputs (run native/AdMob/android-plugin/build.sh).
	func _included(debug: bool) -> bool:
		if not debug and (_setting(APP_ID_SETTING).is_empty() or _setting(UNIT_SETTING).is_empty()):
			return false
		if FileAccess.file_exists("res://addons/" + _library(debug)):
			return true
		push_warning("Rewarded ads left out: run native/AdMob/android-plugin/build.sh to build " + _library(debug))
		return false

	func _get_android_libraries(platform, debug):
		return PackedStringArray([_library(debug)]) if _included(debug) else PackedStringArray()

	func _get_android_dependencies(platform, debug):
		# Keep in step with adsVersion and umpVersion in native/AdMob/android-plugin/build.gradle.
		if not _included(debug):
			return PackedStringArray()
		return PackedStringArray(["com.google.android.gms:play-services-ads:23.6.0", "com.google.android.ump:user-messaging-platform:3.1.0"])

	func _get_android_manifest_application_element_contents(platform, debug):
		if not _included(debug):
			return ""
		var app_id := _setting(APP_ID_SETTING)
		if app_id.is_empty():
			app_id = TEST_APP_ID
		# The SDK stops the app at launch without this.
		return '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="%s" />\n' % app_id

	func _get_name():
		return NAME
