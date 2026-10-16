#!/bin/sh
# Builds the Play Games sign-in plugin and copies the AARs where the Godot export plugin looks.
set -e
cd "$(dirname "$0")"
# Defaults match the JDK 17 and SDK in Godot's Android export settings; Gradle 8.11 cannot run on newer JDKs.
if [ -z "$JAVA_HOME" ] && [ -d /opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home ]; then
    export JAVA_HOME=/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home
fi
if [ -z "$ANDROID_HOME" ]; then
    for sdk in /opt/homebrew/share/android-commandlinetools "$HOME/Library/Android/sdk"; do
        [ -d "$sdk/platforms" ] && export ANDROID_HOME="$sdk" && break
    done
fi
./gradlew --quiet assembleDebug assembleRelease
out=../../addons/PlayGamesSignIn/bin
mkdir -p "$out/debug" "$out/release"
cp build/outputs/aar/PlayGamesSignIn-debug.aar "$out/debug/"
cp build/outputs/aar/PlayGamesSignIn-release.aar "$out/release/"
echo "Built $out/debug/PlayGamesSignIn-debug.aar and $out/release/PlayGamesSignIn-release.aar"
