#include "admob_module.h"

#include "core/config/engine.h"

#include "godot_admob.h"

static CrownroadAds *crownroad_ads = nullptr;

void register_crownroadads_types() {
	crownroad_ads = memnew(CrownroadAds);
	Engine::get_singleton()->add_singleton(Engine::Singleton("CrownroadAds", crownroad_ads));
}

void unregister_crownroadads_types() {
	if (crownroad_ads) {
		memdelete(crownroad_ads);
		crownroad_ads = nullptr;
	}
}
