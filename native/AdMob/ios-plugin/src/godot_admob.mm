#include "godot_admob.h"

#import <GoogleMobileAds/GoogleMobileAds.h>
#import <UIKit/UIKit.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>

CrownroadAds *CrownroadAds::instance = nullptr;

static NSString *unit_id = nil;
static GADRewardedAd *rewarded = nil;
static bool started = false, initialized = false, loading = false;

static void emit(const char *p_signal) {
	CrownroadAds::get_singleton()->call_deferred("emit_signal", p_signal);
}

static void emit_failed(NSString *p_message) {
	CrownroadAds::get_singleton()->call_deferred("emit_signal", "failed", String::utf8(p_message.UTF8String));
}

static UIViewController *root_controller() {
	return UIApplication.sharedApplication.delegate.window.rootViewController;
}

static void load_on_main();

// Emits rewarded-ad completion and loads the next ad once one closes.
@interface CrownroadAdsDelegate : NSObject <GADFullScreenContentDelegate>
@end

@implementation CrownroadAdsDelegate
- (void)adDidDismissFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
	emit("closed");
	load_on_main();
}

- (void)ad:(id<GADFullScreenPresentingAd>)ad didFailToPresentFullScreenContentWithError:(NSError *)error {
	emit_failed([@"Couldn't show the ad: " stringByAppendingString:error.localizedDescription]);
	load_on_main();
}
@end

static CrownroadAdsDelegate *delegate = nil;

static void after_consent() {
	UMPConsentInformation *consent = UMPConsentInformation.sharedInstance;
	bool privacy_required = consent.privacyOptionsRequirementStatus == UMPPrivacyOptionsRequirementStatusRequired;
	CrownroadAds::get_singleton()->call_deferred("emit_signal", "consent", (bool)consent.canRequestAds, privacy_required);
	if (!consent.canRequestAds || initialized) {
		return;
	}
	initialized = true;
	[GADMobileAds.sharedInstance startWithCompletionHandler:^(GADInitializationStatus *status) {
		load_on_main();
	}];
}

static void load_on_main() {
	if (!initialized || loading || rewarded != nil) {
		return;
	}
	loading = true;
	[GADRewardedAd loadWithAdUnitID:unit_id
							request:[GADRequest request]
				  completionHandler:^(GADRewardedAd *ad, NSError *error) {
					  loading = false;
					  if (error != nil) {
						  emit_failed([@"Couldn't load an ad: " stringByAppendingString:error.localizedDescription]);
						  return;
					  }
					  rewarded = ad;
					  emit("loaded");
				  }];
}

void CrownroadAds::start(const String &p_unit_id, bool p_under_age_of_consent) {
	if (started) {
		return;
	}
	started = true;
	unit_id = [NSString stringWithUTF8String:p_unit_id.utf8().get_data()];
	dispatch_async(dispatch_get_main_queue(), ^{
		UMPRequestParameters *parameters = [[UMPRequestParameters alloc] init];
		parameters.tagForUnderAgeOfConsent = p_under_age_of_consent;
		[UMPConsentInformation.sharedInstance requestConsentInfoUpdateWithParameters:parameters
																   completionHandler:^(NSError *request_error) {
																	   if (request_error != nil) {
																		   after_consent();
																		   return;
																	   }
																	   [UMPConsentForm loadAndPresentIfRequiredFromViewController:root_controller()
																								 completionHandler:^(NSError *form_error) {
																									 after_consent();
																								 }];
																   }];
	});
}

void CrownroadAds::load() {
	dispatch_async(dispatch_get_main_queue(), ^{
		load_on_main();
	});
}

void CrownroadAds::show() {
	dispatch_async(dispatch_get_main_queue(), ^{
		GADRewardedAd *shown = rewarded;
		if (shown == nil) {
			emit_failed(@"No ad is ready.");
			return;
		}
		rewarded = nil;
		shown.fullScreenContentDelegate = delegate;
		[shown presentFromRootViewController:root_controller()
					userDidEarnRewardHandler:^{
						GADAdReward *reward = shown.adReward;
						CrownroadAds::get_singleton()->call_deferred("emit_signal", "rewarded", String::utf8(reward.type.UTF8String), reward.amount.intValue);
					}];
	});
}

void CrownroadAds::privacy() {
	dispatch_async(dispatch_get_main_queue(), ^{
		[UMPConsentForm presentPrivacyOptionsFormFromViewController:root_controller()
												  completionHandler:^(NSError *form_error) {
													  after_consent();
												  }];
	});
}

void CrownroadAds::_bind_methods() {
	ClassDB::bind_method(D_METHOD("start", "unit_id", "under_age_of_consent"), &CrownroadAds::start);
	ClassDB::bind_method(D_METHOD("load"), &CrownroadAds::load);
	ClassDB::bind_method(D_METHOD("show"), &CrownroadAds::show);
	ClassDB::bind_method(D_METHOD("privacy"), &CrownroadAds::privacy);

	ADD_SIGNAL(MethodInfo("consent", PropertyInfo(Variant::BOOL, "can_request_ads"), PropertyInfo(Variant::BOOL, "privacy_options_required")));
	ADD_SIGNAL(MethodInfo("loaded"));
	ADD_SIGNAL(MethodInfo("failed", PropertyInfo(Variant::STRING, "message")));
	ADD_SIGNAL(MethodInfo("rewarded", PropertyInfo(Variant::STRING, "type"), PropertyInfo(Variant::INT, "amount")));
	ADD_SIGNAL(MethodInfo("closed"));
}

CrownroadAds *CrownroadAds::get_singleton() {
	return instance;
}

CrownroadAds::CrownroadAds() {
	ERR_FAIL_COND(instance != nullptr);
	instance = this;
	delegate = [[CrownroadAdsDelegate alloc] init];
}

CrownroadAds::~CrownroadAds() {
	if (instance == this) {
		instance = nullptr;
	}
}
