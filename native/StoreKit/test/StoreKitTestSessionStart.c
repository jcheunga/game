// Starts StoreKitTestSession.swift when the library loads, before the game's own code runs.
extern void cr_storekit_test_start(void);

__attribute__((constructor)) static void start(void) {
	cr_storekit_test_start();
}
