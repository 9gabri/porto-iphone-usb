#!/bin/bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin ]]; then
    echo 'Occorre macOS con Xcode e il relativo SDK iPhoneOS.' >&2
    exit 1
fi
if [[ $# != 1 ]]; then
    echo 'Uso: bash build-iphone.sh NUOVA_CARTELLA_DI_OUTPUT' >&2
    exit 2
fi
build_dir="$(cd "$(dirname "$0")" && pwd)"
source_dir="$build_dir/../Phone"
output="$1"
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
sdk_version="$(xcrun --sdk iphoneos --show-sdk-version)"
stage="$(mktemp -d "${TMPDIR:-/tmp}/porto-iphone.XXXXXXXX")"
trap 'rm -rf "$stage"' EXIT
app="$stage/Payload/PortoAlbum.app"
mkdir -p "$app"
python3 - "$source_dir/Info.plist" "$app/Info.plist" "$sdk_version" <<'PY'
import plistlib
import sys
with open(sys.argv[1], "rb") as source:
    info = plistlib.load(source)
if info.get("CFBundleExecutable") != "PortoAlbum" or info.get("MinimumOSVersion") != "17.0":
    raise ValueError("Nome eseguibile o versione iOS diversi dal target di compilazione.")
info.update(CFBundleSupportedPlatforms=["iPhoneOS"], DTPlatformName="iphoneos", DTSDKName="iphoneos" + sys.argv[3])
with open(sys.argv[2], "wb") as out:
    plistlib.dump(info, out, fmt=plistlib.FMT_BINARY)
PY
sources=("$source_dir"/*.swift)
xcodebuild -version
xcrun --sdk iphoneos swiftc -version
xcrun --sdk iphoneos swiftc -sdk "$sdk" -target arm64-apple-ios17.0 -swift-version 5 \
    -parse-as-library -module-name PortoAlbum -O -whole-module-optimization \
    -framework UIKit -framework Photos -framework CryptoKit -framework Security \
    -Xlinker -rpath -Xlinker /usr/lib/swift "${sources[@]}" -o "$app/PortoAlbum"

# An ad hoc seal is independent of any Apple account. The installer supplies the
# personal certificate, entitlements and provisioning profile for the actual phone.
codesign --force --sign - --timestamp=none "$app"
codesign --verify --strict --verbose=2 "$app"
(cd "$stage" && /usr/bin/zip -qr PortoAlbum-per-firma.ipa Payload)
python3 "$build_dir/verify-ipa.py" "$stage/PortoAlbum-per-firma.ipa"
mkdir -p "$(dirname "$output")"
mkdir "$output"
mv "$stage/PortoAlbum-per-firma.ipa" "$output/"
shasum -a 256 "$output/PortoAlbum-per-firma.ipa"
