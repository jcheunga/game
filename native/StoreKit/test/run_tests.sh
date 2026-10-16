#!/bin/bash
# Checks that the plugin's appAccountToken matches the server's for every vector in
# server/testdata/apple_app_account_tokens.json. The server tests check the same file against
# ProfileAppAccountToken (dotnet run -- --test in server/). Runs on macOS with Xcode.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../build/test"
mkdir -p "$out"
xcrun --sdk macosx swiftc -parse-as-library -O -o "$out/app_account_token_tests" \
	"$here/../src/AppAccountToken.swift" "$here/AppAccountTokenTests.swift"
"$out/app_account_token_tests" "$here/../../../server/testdata/apple_app_account_tokens.json"
