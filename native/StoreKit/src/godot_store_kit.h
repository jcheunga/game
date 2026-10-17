#ifndef GODOT_STORE_KIT_H
#define GODOT_STORE_KIT_H

#include "core/object/class_db.h"

// The `StoreKit` engine singleton. It forwards calls to StoreKitBridge.swift and turns the
// bridge's events into signals (see README.md for the API).
class GodotStoreKit : public Object {
	GDCLASS(GodotStoreKit, Object);

	static GodotStoreKit *instance;

protected:
	static void _bind_methods();

public:
	bool configure(const String &p_profile_id);
	void request_products(const Array &p_product_ids);
	void purchase(const String &p_product_id);
	bool finish_transaction(const String &p_transaction_id);
	void check_unfinished();

	void handle_event(const Dictionary &p_event);

	static GodotStoreKit *get_singleton();

	GodotStoreKit();
	~GodotStoreKit();
};

#endif
