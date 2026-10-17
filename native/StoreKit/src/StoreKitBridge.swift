import Foundation
import StoreKit
import UIKit

/// StoreKit 2 for the game's shop. godot_store_kit.mm wraps it in the `StoreKit` engine singleton.
///
/// Every event goes to `eventHandler` on the main thread as a dictionary with a "type":
///
/// - "products": `products` (one dictionary per product) and `invalid_product_ids`.
/// - "products_failed": `message`.
/// - "transaction": a verified, unfinished transaction for the game to validate with the server.
///   `restored` is false for the answer to `purchase(_:)` and true for one found at launch,
///   approved later (Ask to Buy) or bought outside the game.
/// - "purchase_failed": `product_id`, `reason` (cancelled, pending, not_configured, busy,
///   unknown_product, unverified, not_allowed, network or error) and `message`.
/// - "transaction_finished": `transaction_id`.
///
/// A transaction is never finished here on its own. The game calls `finishTransaction(_:)` once the
/// server has recorded the grant, so a crash or a failed validation leaves it in
/// `Transaction.unfinished`, where `checkUnfinished()` finds it again.
@MainActor
@objc(CRStoreKitBridge)
public final class StoreKitBridge: NSObject {
	@objc public var eventHandler: (([String: Any]) -> Void)?

	private var appAccountToken: UUID?
	private var products: [String: Product] = [:]
	/// Transactions handed to the game and not yet finished, by transaction ID.
	private var delivered: [UInt64: Transaction] = [:]
	private var purchasing: Set<String> = []
	private var updates: Task<Void, Never>?

	/// Sets the player whose profile every purchase is tied to (see AppAccountToken.swift).
	/// Returns false for a blank profile ID, which leaves purchases disabled.
	@objc public func configure(profileId: String) -> Bool {
		appAccountToken = AppAccountToken.forProfile(profileId)
		return appAccountToken != nil
	}

	@objc public func requestProducts(_ productIds: [String]) {
		Task {
			do {
				let loaded = try await Product.products(for: productIds)
				for product in loaded {
					products[product.id] = product
				}
				let found = Set(loaded.map(\.id))
				send([
					"type": "products",
					"products": loaded.map(describe),
					"invalid_product_ids": productIds.filter { !found.contains($0) },
				])
			} catch {
				send(["type": "products_failed", "message": error.localizedDescription])
			}
		}
	}

	@objc public func purchase(_ productId: String) {
		guard let token = appAccountToken else {
			purchaseFailed(productId, "not_configured", "The store is not ready yet. Try again in a moment.")
			return
		}
		guard purchasing.insert(productId).inserted else {
			purchaseFailed(productId, "busy", "This purchase is already in progress.")
			return
		}
		Task {
			defer { purchasing.remove(productId) }
			do {
				guard let product = try await product(productId) else {
					purchaseFailed(productId, "unknown_product", "This item is not available in the App Store.")
					return
				}
				switch try await buy(product, token: token) {
				case .success(let verification):
					if case .unverified(_, let error) = verification {
						NSLog("[StoreKit] %@ failed verification: %@", productId, String(describing: error))
						purchaseFailed(productId, "unverified", "The App Store could not confirm this purchase.")
					} else {
						deliver(verification, restored: false, again: true)
					}
				case .userCancelled:
					purchaseFailed(productId, "cancelled", "The purchase was cancelled.")
				case .pending:
					purchaseFailed(productId, "pending",
						"The purchase is waiting for approval. It will be added once it is approved.")
				@unknown default:
					purchaseFailed(productId, "error", "The App Store returned an unexpected result.")
				}
			} catch {
				NSLog("[StoreKit] Purchase of %@ failed: %@", productId, String(reflecting: error))
				purchaseFailed(productId, reason(for: error), error.localizedDescription)
			}
		}
	}

	/// Finishes a transaction the server has recorded. Returns false if it is not one this session
	/// handed over; the transaction then stays unfinished and comes back on the next check.
	@objc public func finishTransaction(_ transactionId: String) -> Bool {
		guard let id = UInt64(transactionId), let transaction = delivered[id] else {
			return false
		}
		Task {
			await transaction.finish()
			delivered[id] = nil
			send(["type": "transaction_finished", "transaction_id": transactionId])
		}
		return true
	}

	/// Starts listening to `Transaction.updates` and hands over every unfinished transaction. Later
	/// calls hand over again the ones the game has already seen but could not validate yet.
	@objc public func checkUnfinished() {
		// At launch `Transaction.updates` reports unfinished transactions too, so the first scan
		// skips any it has already handed over.
		let again = updates != nil
		if updates == nil {
			updates = Task { [weak self] in
				for await update in Transaction.updates {
					self?.deliver(update, restored: true, again: false)
				}
			}
		}
		Task {
			for await result in Transaction.unfinished {
				deliver(result, restored: true, again: again)
			}
		}
	}

	/// Hands a transaction to the game. `again` re-sends one already handed over this session;
	/// without it a transaction that both `Transaction.unfinished` and `Transaction.updates`
	/// report at launch would be validated twice.
	private func deliver(_ result: VerificationResult<Transaction>, restored: Bool, again: Bool) {
		guard case .verified(let transaction) = result else {
			if case .unverified(let transaction, let error) = result {
				NSLog("[StoreKit] Ignoring unverified transaction %llu: %@", transaction.id, String(describing: error))
			}
			return
		}
		if transaction.revocationDate != nil {
			// Refunded or revoked before the game recorded it: there is nothing to grant.
			Task { await transaction.finish() }
			return
		}
		if !again && delivered[transaction.id] != nil {
			return
		}
		delivered[transaction.id] = transaction
		send([
			"type": "transaction",
			"product_id": transaction.productID,
			"transaction_id": String(transaction.id),
			"original_transaction_id": String(transaction.originalID),
			"receipt": result.jwsRepresentation,
			"app_account_token": transaction.appAccountToken?.uuidString.lowercased() ?? "",
			"purchase_date": transaction.purchaseDate.timeIntervalSince1970,
			"environment": environment(of: transaction),
			"restored": restored,
		])
	}

	private func product(_ productId: String) async throws -> Product? {
		if let product = products[productId] {
			return product
		}
		let product = try await Product.products(for: [productId]).first
		products[productId] = product
		return product
	}

	private func buy(_ product: Product, token: UUID) async throws -> Product.PurchaseResult {
		let options: Set<Product.PurchaseOption> = [.appAccountToken(token)]
		if #available(iOS 18.2, *), let controller = rootViewController() {
			return try await product.purchase(confirmIn: controller, options: options)
		}
		return try await product.purchase(options: options)
	}

	private func rootViewController() -> UIViewController? {
		let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
		for scene in scenes where scene.activationState == .foregroundActive {
			if let controller = scene.keyWindow?.rootViewController {
				return controller
			}
		}
		return UIApplication.shared.delegate?.window??.rootViewController
	}

	private func describe(_ product: Product) -> [String: Any] {
		let micros = NSDecimalNumber(decimal: product.price).multiplying(byPowerOf10: 6)
		return [
			"product_id": product.id,
			"localized_title": product.displayName,
			"localized_description": product.description,
			"localized_price": product.displayPrice,
			"price_micros": micros.int64Value,
			"currency_code": product.priceFormatStyle.currencyCode,
			"type": product.type.rawValue,
		]
	}

	private func environment(of transaction: Transaction) -> String {
		if #available(iOS 16.0, *) {
			return transaction.environment.rawValue
		}
		return transaction.environmentStringRepresentation
	}

	private func reason(for error: Error) -> String {
		switch error {
		case StoreKitError.userCancelled:
			return "cancelled"
		case StoreKitError.networkError:
			return "network"
		case Product.PurchaseError.purchaseNotAllowed:
			return "not_allowed"
		default:
			// StoreKit 1 codes, which some failures still arrive as.
			switch (error as? SKError)?.code {
			case .paymentCancelled?: return "cancelled"
			case .paymentNotAllowed?: return "not_allowed"
			case .cloudServiceNetworkConnectionFailed?: return "network"
			default: return "error"
			}
		}
	}

	private func purchaseFailed(_ productId: String, _ reason: String, _ message: String) {
		send(["type": "purchase_failed", "product_id": productId, "reason": reason, "message": message])
	}

	private func send(_ event: [String: Any]) {
		eventHandler?(event)
	}
}
