extends Node

## Adapts the maintained Godot Google Play Billing plug-in to the small,
## platform-neutral surface that NativeIAPService consumes. The bridge is kept
## in GDScript because the plug-in's current public API is GDScript-first.
##
## Consumables are intentionally *not* consumed here. The C# service calls
## confirm_consumed only after the backend has verified and recorded the
## purchase, so a force-close cannot silently lose a paid transaction.

signal billing_ready
signal billing_unavailable(message: String)
signal product_details_received(details: Array)
signal purchase_received(product_id: String, transaction_id: String, purchase_token: String, restored: bool)
signal purchase_failed(message: String)
signal consume_finished(purchase_token: String, success: bool, message: String)

var _billing: BillingClient
var _product_ids := PackedStringArray()
var _profile_id := ""
var _is_connected := false


func _ready() -> void:
	if not OS.has_feature("android"):
		return
	if not Engine.has_singleton("GodotGooglePlayBilling"):
		billing_unavailable.emit("Google Play Billing plug-in is unavailable in this Android build.")
		return

	_billing = BillingClient.new()
	add_child(_billing)
	_billing.connected.connect(_on_connected)
	_billing.disconnected.connect(_on_disconnected)
	_billing.connect_error.connect(_on_connect_error)
	_billing.query_product_details_response.connect(_on_product_details_response)
	_billing.query_purchases_response.connect(_on_purchase_query_response)
	_billing.on_purchase_updated.connect(_on_purchase_updated)
	_billing.consume_purchase_response.connect(_on_consume_response)
	_billing.start_connection()


func configure(product_ids: Array, profile_id: String) -> void:
	# C# marshals Godot.Collections.Array<string> as a generic Array rather
	# than PackedStringArray. Normalize it at this boundary so the native
	# plug-in always receives its expected packed list.
	_product_ids = PackedStringArray()
	for product_id in product_ids:
		_product_ids.append(str(product_id))
	_profile_id = profile_id
	if _is_connected:
		configure_and_query()


func is_available() -> bool:
	return _billing != null and _is_connected and _billing.is_ready()


func begin_purchase(product_id: String) -> void:
	if not is_available():
		purchase_failed.emit("Google Play Billing is not ready.")
		return
	var result: Dictionary = _billing.purchase(product_id)
	if int(result.get("response_code", -1)) != BillingClient.BillingResponseCode.OK:
		purchase_failed.emit(str(result.get("debug_message", "Google Play did not start the purchase.")))


func confirm_consumed(purchase_token: String) -> void:
	if not is_available() or purchase_token.is_empty():
		return
	_billing.consume_purchase(purchase_token)


func _on_connected() -> void:
	_is_connected = true
	configure_and_query()
	billing_ready.emit()


func configure_and_query() -> void:
	if _billing == null:
		return
	# The identifier is one-way hashed before Play receives it. The server
	# compares the same SHA-256 fingerprint when validating a purchase.
	if not _profile_id.is_empty():
		_billing.set_obfuscated_account_id(_profile_id.sha256_text())
	if not _product_ids.is_empty():
		_billing.query_product_details(_product_ids, BillingClient.ProductType.INAPP)
	# Re-surface an unfinished transaction after an interruption. It still must
	# pass server validation before it is consumed or granted.
	_billing.query_purchases(BillingClient.ProductType.INAPP)


func _on_disconnected() -> void:
	_is_connected = false
	billing_unavailable.emit("Google Play Billing disconnected.")


func _on_connect_error(response_code: int, debug_message: String) -> void:
	_is_connected = false
	billing_unavailable.emit("Google Play Billing error %d: %s" % [response_code, debug_message])


func _on_product_details_response(response: Dictionary) -> void:
	if int(response.get("response_code", -1)) != BillingClient.BillingResponseCode.OK:
		billing_unavailable.emit(str(response.get("debug_message", "Unable to load Google Play products.")))
		return
	product_details_received.emit(response.get("product_details", []))


func _on_purchase_updated(response: Dictionary) -> void:
	_handle_purchase_response(response, false)


func _on_purchase_query_response(response: Dictionary) -> void:
	_handle_purchase_response(response, true)


func _handle_purchase_response(response: Dictionary, restored: bool) -> void:
	if int(response.get("response_code", -1)) != BillingClient.BillingResponseCode.OK:
		if not restored:
			purchase_failed.emit(str(response.get("debug_message", "Google Play purchase failed.")))
		return
	for purchase in response.get("purchases", []):
		if int(purchase.get("purchase_state", -1)) != BillingClient.PurchaseState.PURCHASED:
			if not restored:
				purchase_failed.emit("The purchase is pending confirmation in Google Play.")
			continue
		var ids = purchase.get("product_ids", [])
		if ids.is_empty():
			continue
		purchase_received.emit(
			str(ids[0]),
			str(purchase.get("order_id", "")),
			str(purchase.get("purchase_token", "")),
			restored
		)


func _on_consume_response(response: Dictionary) -> void:
	var success := int(response.get("response_code", -1)) == BillingClient.BillingResponseCode.OK
	consume_finished.emit(
		str(response.get("token", "")),
		success,
		str(response.get("debug_message", ""))
	)
