import Foundation

// Checks src/AppAccountToken.swift against the vectors the server tests check against
// ProfileAppAccountToken, so the token a purchase carries is the one the server expects.
// Built and run by run_tests.sh.

@main
struct AppAccountTokenTests {
	static func main() throws {
		let path = CommandLine.arguments[1]
		let data = try Data(contentsOf: URL(fileURLWithPath: path))
		let root = try JSONSerialization.jsonObject(with: data) as! [String: Any]
		let vectors = root["vectors"] as! [[String: String]]
		var failures = 0
		for vector in vectors {
			let profileId = vector["profileId"]!
			let expected = vector["appAccountToken"]!
			let actual = AppAccountToken.string(forProfile: profileId)
			if actual != expected {
				failures += 1
				print("FAIL  \(profileId.debugDescription): \(actual), expected \(expected)")
			}
		}
		if AppAccountToken.forProfile(" \n") != nil {
			failures += 1
			print("FAIL  a blank profile ID must not produce a token")
		}
		print("\(vectors.count - failures) of \(vectors.count) appAccountToken vectors match the server")
		exit(failures == 0 ? 0 : 1)
	}
}
