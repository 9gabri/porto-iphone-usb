#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
phone="$here/../Phone"
work="$(mktemp -d)"
bundle="it.porto.test.photosmoke"
app="$work/Smoke.app"
mkdir "$app"
cat > "$work/tls.cnf" <<'CONF'
[req]
prompt=no
distinguished_name=dn
x509_extensions=extensions
[dn]
CN=Porto Album
[extensions]
subjectAltName=IP:127.0.0.1
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
CONF
openssl req -config "$work/tls.cnf" -x509 -newkey rsa:2048 -nodes -sha256 -days 2 -keyout "$work/key.pem" -out "$work/cert.pem"
pin="$(openssl x509 -in "$work/cert.pem" -outform der | shasum -a 256 | cut -d' ' -f1)"
python3 - "$phone/Info.plist" "$work/Info.plist" "$bundle" "$pin" <<'PY'
import sys, plistlib
with open(sys.argv[1], 'rb') as f:
    info = plistlib.load(f)
info.update(CFBundleIdentifier=sys.argv[3], SmokePin=sys.argv[4], CFBundleExecutable="Smoke", CFBundleSupportedPlatforms=["iPhoneSimulator"],
            NSPhotoLibraryAddUsageDescription="Crea due immagini solo nel simulatore di prova.")
with open(sys.argv[2], 'wb') as f:
    plistlib.dump(info, f)
PY
cp "$phone/Transfer.swift" "$here/PhotoKitSmoke.swift" "$here/SmokeUITest.swift" "$work"
if ! ruby -rxcodeproj -e '' 2>/dev/null; then
    export GEM_HOME="$work/gems"
    gem install xcodeproj -v 1.27.0 --no-document
fi
ruby "$here/smoke-project.rb" "$work"
device="$(xcrun simctl create "Porto album smoke" com.apple.CoreSimulator.SimDeviceType.iPhone-16 com.apple.CoreSimulator.SimRuntime.iOS-18-5)"
python3 "$here/smoke-receiver.py" "$work" &
server_pid=$!
trap 'kill "$server_pid" || true; xcrun simctl shutdown "$device" >/dev/null 2>&1 || true; xcrun simctl delete "$device"' EXIT
for attempt in {1..10}; do [[ -f "$work/peer-ready" ]] && break; sleep 1; done
test -f "$work/peer-ready"
xcrun simctl boot "$device"
xcrun simctl bootstatus "$device" -b
xcodebuild -project "$work/Smoke.xcodeproj" -scheme Smoke -configuration Debug -parallel-testing-enabled NO \
  -destination "platform=iOS Simulator,id=$device" -derivedDataPath "$work/derived" test
