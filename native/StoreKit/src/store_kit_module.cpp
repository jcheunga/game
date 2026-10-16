#include "store_kit_module.h"

#include "core/config/engine.h"

#include "godot_store_kit.h"

static GodotStoreKit *store_kit = nullptr;

void register_storekit_types() {
	store_kit = memnew(GodotStoreKit);
	Engine::get_singleton()->add_singleton(Engine::Singleton("StoreKit", store_kit));
}

void unregister_storekit_types() {
	if (store_kit) {
		memdelete(store_kit);
		store_kit = nullptr;
	}
}
