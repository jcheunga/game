#ifndef GODOT_ADMOB_H
#define GODOT_ADMOB_H

#include "core/object/class_db.h"

// The `CrownroadAds` engine singleton: AdMob rewarded ads after Google's consent form (UMP), for the game's
// RewardedAds. Every call runs on the main thread, and each result comes back as a signal: consent(can_request_ads,
// privacy_options_required), loaded, failed(message), rewarded(type, amount), closed. The Android plugin
// (native/AdMob/android-plugin) has the same methods and signals.
class CrownroadAds : public Object {
	GDCLASS(CrownroadAds, Object);

	static CrownroadAds *instance;

protected:
	static void _bind_methods();

public:
	void start(const String &p_unit_id, bool p_under_age_of_consent);
	void load();
	void show();
	void privacy();

	static CrownroadAds *get_singleton();

	CrownroadAds();
	~CrownroadAds();
};

#endif
