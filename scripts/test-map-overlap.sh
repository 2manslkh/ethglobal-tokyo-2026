#!/usr/bin/env bash
set -euo pipefail
if [[ $# -lt 1 || $# -gt 2 ]]; then
    echo "Usage: $0 SIMULATOR_UDID [--review]" >&2
    exit 2
fi
simulator_id=$1
review_argument=${2:-}
source_root=$(cd "$(dirname "$0")/.." && pwd -P)
build_dir=$(mktemp -d /private/tmp/tagtag-map-overlap-test.XXXXXX)
app_dir="$build_dir/MapOverlap.app"
mkdir -p "$app_dir"
cp "$source_root/Assets/Resources/Tagtag/Presets/"taggi-*.png "$app_dir/"
sdk_path=$(xcrun --sdk iphonesimulator --show-sdk-path)
xcrun --sdk iphonesimulator clang++ -fobjc-arc -fmodules -std=c++17 \
    -target arm64-apple-ios15.0-simulator -isysroot "$sdk_path" \
    -I "$source_root/scripts/tests" \
    -framework UIKit -framework MapKit -framework Foundation -framework CoreGraphics -framework CoreLocation \
    "$source_root/scripts/tests/MapOverlap.mm" -o "$app_dir/MapOverlap"
cat > "$app_dir/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.kenk.tagtag.map-overlap-tests</string>
<key>CFBundleExecutable</key><string>MapOverlap</string>
<key>CFBundleName</key><string>MapOverlap</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>MinimumOSVersion</key><string>15.0</string>
<key>LSRequiresIPhoneOS</key><true/>
<key>UIDeviceFamily</key><array><integer>1</integer></array>
<key>UISupportedInterfaceOrientations</key><array><string>UIInterfaceOrientationPortrait</string></array>
<key>UILaunchScreen</key><dict/>
</dict></plist>
PLIST
xcrun simctl install "$simulator_id" "$app_dir"
container=$(xcrun simctl get_app_container "$simulator_id" com.kenk.tagtag.map-overlap-tests data)
result="$container/Documents/result.json"
rm -f "$result"
if [[ -n "$review_argument" ]]; then
    xcrun simctl launch --terminate-running-process "$simulator_id" com.kenk.tagtag.map-overlap-tests "$review_argument"
else
    xcrun simctl launch --terminate-running-process "$simulator_id" com.kenk.tagtag.map-overlap-tests
fi
for ((attempt = 0; attempt < 80; attempt++)); do
    if [[ -f "$result" ]]; then
        cp "$result" "$build_dir/result.json"
        echo "Artifacts: $build_dir"
        python3 - "$result" <<'PY'
import json, sys
result = json.load(open(sys.argv[1]))
print(result['message'])
sys.exit(0 if result['passed'] else 1)
PY
        exit 0
    fi
    sleep .25
done
echo "Native map test timed out. Artifacts: $build_dir" >&2
exit 1
