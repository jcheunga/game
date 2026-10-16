import CryptoKit
import Foundation

/// The UUID a purchase carries as its `appAccountToken`, which ties an App Store transaction to a
/// player profile. It must equal `ProfileAppAccountToken` in server/StorePurchaseVerification.cs:
/// the first 16 bytes of SHA-256 of the trimmed profile ID, with the version nibble set to 5 and
/// the RFC 4122 variant bits, turned into a string by .NET's `new Guid(bytes).ToString()`.
///
/// That constructor reads the first three fields (4, 2 and 2 bytes) little-endian, so those bytes
/// are reversed here before they become a UUID. As a result the "5" lands in the third character of
/// the third group rather than where RFC 4122 puts the version, which StoreKit does not mind: it
/// accepts any UUID, and the server only compares strings.
///
/// test/run_tests.sh checks this against the vectors in server/testdata, which the server tests
/// check against its own implementation.
enum AppAccountToken {
	static func forProfile(_ profileId: String) -> UUID? {
		// Same characters as .NET's string.Trim(): Unicode spaces and separators, plus tab,
		// line feed through carriage return and U+0085.
		let trimmed = profileId.trimmingCharacters(in: .whitespacesAndNewlines)
		if trimmed.isEmpty {
			return nil
		}
		var b = Array(SHA256.hash(data: Data(trimmed.utf8)).prefix(16))
		b[6] = (b[6] & 0x0f) | 0x50
		b[8] = (b[8] & 0x3f) | 0x80
		return UUID(uuid: (b[3], b[2], b[1], b[0], b[5], b[4], b[7], b[6],
			b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]))
	}

	/// The lowercase form .NET's `Guid.ToString()` produces.
	static func string(forProfile profileId: String) -> String {
		forProfile(profileId)?.uuidString.lowercased() ?? ""
	}
}
