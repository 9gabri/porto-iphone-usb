#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
phone="$here/../Phone"
work="$(mktemp -d)"
bundle="it.porto.test.photosmoke"
app="$work/Smoke.app"
mkdir "$app"
sdk="$(xcrun --sdk iphonesimulator --show-sdk-path)"
xcrun --sdk iphonesimulator swiftc -parse-as-library -swift-version 5 -target "$(uname -m)-apple-ios17.0-simulator" \
  -sdk "$sdk" "$phone/Transfer.swift" "$here/PhotoKitSmoke.swift" -o "$app/Smoke"
python3 - "$phone/Info.plist" "$app/Info.plist" "$bundle" <<'PY'
import sys, plistlib
with open(sys.argv[1], 'rb') as f:
    info = plistlib.load(f)
info.update(CFBundleIdentifier=sys.argv[3], CFBundleExecutable="Smoke", CFBundleSupportedPlatforms=["iPhoneSimulator"],
            NSPhotoLibraryAddUsageDescription="Crea due immagini solo nel simulatore di prova.")
with open(sys.argv[2], 'wb') as f:
    plistlib.dump(info, f)
PY
codesign --force --sign - "$app"
device="$(xcrun simctl create "Porto album smoke" com.apple.CoreSimulator.SimDeviceType.iPhone-16 com.apple.CoreSimulator.SimRuntime.iOS-18-5)"
trap 'xcrun simctl shutdown "$device" >/dev/null 2>&1 || true; xcrun simctl delete "$device"' EXIT
xcrun simctl boot "$device"
xcrun simctl bootstatus "$device" -b
xcrun simctl install "$device" "$app"
xcrun simctl privacy "$device" grant photos "$bundle"
xcrun simctl launch "$device" "$bundle"
data="$(xcrun simctl get_app_container "$device" "$bundle" data)"
for attempt in {1..90}; do
    if [[ -f "$data/Documents/esito.txt" ]]; then
        cat "$data/Documents/esito.txt"
        grep -qx OK "$data/Documents/esito.txt"
        exit 0
    fi
    sleep 2
done
echo 'Simulatore privo di esito nei tempi previsti' >&2
exit 1
