import Foundation
import StoreKit
import StoreKitTest
import notify

// Loaded into the game on a simulator by simulator_e2e.sh (DYLD_INSERT_LIBRARIES, started by
// the constructor in StoreKitTestSessionStart.c). It opens an SKTestSession from Crownroad.storekit,
// which is what running from Xcode with that configuration does, so the shop buys from the local
// configuration without Xcode's window. Purchase sheets are confirmed automatically.
//
// Environment, read once at launch:
//   CR_STOREKIT_CONFIG        path to the .storekit file (required)
//   CR_STOREKIT_RESET=1       start with no transactions and no simulated errors
//   CR_STOREKIT_ASK_TO_BUY=1  purchases wait for approval (Ask to Buy)
//   CR_STOREKIT_FAIL=unverified
//                             purchases' signatures fail verification
//
// While the game runs, `xcrun simctl spawn booted notifyutil -p <name>` sends:
//   com.crownroad.storekit-test.approve   approve every purchase waiting for Ask to Buy
//   com.crownroad.storekit-test.list      log every transaction
// Lines are logged with a "[StoreKitTest]" prefix.

private var session: SKTestSession?
private var tokens: [Int32] = []

private func log(_ message: String) {
	NSLog("[StoreKitTest] %@", message)
}

/// Runs async session setup to completion before the game starts, so its first StoreKit call
/// already sees the test configuration.
private func waitFor(_ work: @escaping @Sendable () async throws -> Void) {
	let done = DispatchSemaphore(value: 0)
	Task.detached {
		do { try await work() } catch { log("Setup failed: \(error)") }
		done.signal()
	}
	done.wait()
}

private func approveAskToBuy() {
	guard let session else { return }
	for transaction in session.allTransactions() where transaction.pendingAskToBuyConfirmation {
		do {
			try session.approveAskToBuyTransaction(identifier: transaction.identifier)
			log("Approved Ask to Buy for transaction \(transaction.identifier) (\(transaction.productIdentifier))")
		} catch {
			log("Could not approve Ask to Buy for \(transaction.identifier): \(error)")
		}
	}
}

private func listen(_ name: String, _ handler: @escaping () -> Void) {
	var token: Int32 = 0
	notify_register_dispatch(name, &token, DispatchQueue.main) { _ in handler() }
	tokens.append(token)
}

@_cdecl("cr_storekit_test_start")
public func startStoreKitTestSession() {
	let environment = ProcessInfo.processInfo.environment
	guard let path = environment["CR_STOREKIT_CONFIG"], !path.isEmpty else { return }
	let opened: SKTestSession
	do {
		opened = try SKTestSession(contentsOf: URL(fileURLWithPath: path))
	} catch {
		log("Could not open \(path): \(error)")
		return
	}
	session = opened
	if environment["CR_STOREKIT_RESET"] == "1" {
		opened.resetToDefaultState()
		opened.clearTransactions()
	}
	opened.disableDialogs = true
	opened.askToBuyEnabled = environment["CR_STOREKIT_ASK_TO_BUY"] == "1"
	let failure = environment["CR_STOREKIT_FAIL"] ?? ""
	waitFor {
		// A simulated error outlives the launch, so each launch sets it.
		try await opened.setSimulatedError(failure == "unverified" ? .verification(.invalidSignature) : nil, forAPI: .verification)
	}
	log("Session open with \((path as NSString).lastPathComponent): Ask to Buy \(opened.askToBuyEnabled ? "on" : "off"), failure \(failure.isEmpty ? "none" : failure)")
	listen("com.crownroad.storekit-test.approve") { approveAskToBuy() }
	listen("com.crownroad.storekit-test.list") {
		for transaction in session?.allTransactions() ?? [] {
			log("Transaction \(transaction.identifier) \(transaction.productIdentifier) state \(transaction.state.rawValue) askToBuy \(transaction.pendingAskToBuyConfirmation)")
		}
	}
}
