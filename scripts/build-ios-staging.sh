#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 || ! -f $1 || ! -f $2 ]]; then
    echo "Usage: $0 /absolute/path/to/staging-service-configuration.json /absolute/path/to/GoogleService-Info.plist" >&2
    exit 2
fi

source_project=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)
config_directory=$(cd "$(dirname "$1")" && pwd -P)
export TAGTAG_STAGING_CONFIG="$config_directory/$(basename "$1")"
firebase_directory=$(cd "$(dirname "$2")" && pwd -P)
export TAGTAG_STAGING_FIREBASE_PLIST="$firebase_directory/$(basename "$2")"
unity_editor=${TAGTAG_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity}
if [[ ! -x $unity_editor ]]; then
    echo "Unity Editor is not executable: $unity_editor" >&2
    exit 2
fi
if [[ -e $source_project/Build/iOS-staging ]]; then
    echo "Build/iOS-staging already exists; choose a clean output before exporting." >&2
    exit 2
fi

# Reject the unfinished example and accidental production settings before Unity imports a copy.
python3 - "$TAGTAG_STAGING_CONFIG" "$source_project/Assets/Resources/Tagtag/ServiceConfiguration.json" "$TAGTAG_STAGING_FIREBASE_PLIST" <<'PY'
import json
import plistlib
import sys

try:
    with open(sys.argv[1], encoding='utf-8') as source:
        staging = json.load(source)
    with open(sys.argv[2], encoding='utf-8') as source:
        production = json.load(source)
    with open(sys.argv[3], 'rb') as source:
        firebase = plistlib.load(source)
    client = staging['googleClientId']
    suffix = '.apps.googleusercontent.com'
    app_id = firebase['GOOGLE_APP_ID'].split(':')
    valid = (
        staging['apiBaseUrl'] == 'https://tagtag-api-542095619867.asia-northeast1.run.app'
        and staging['nftEnabled'] is True
        and bool(staging['firebaseApiKey'])
        and bool(staging['thirdwebClientId'])
        and client.endswith(suffix)
        and staging['googleReversedClientId'] == 'com.googleusercontent.apps.' + client[:-len(suffix)]
        and production['nftEnabled'] is False
        and staging['firebaseApiKey'] != production['firebaseApiKey']
        and client != production['googleClientId']
        and staging['googleReversedClientId'] != production['googleReversedClientId']
        and firebase['PROJECT_ID'] == 'tagtag-nft-staging-2026'
        and firebase['GCM_SENDER_ID'] == '542095619867'
        and len(app_id) == 4
        and app_id[0:3] == ['1', '542095619867', 'ios']
        and bool(app_id[3])
        and firebase['BUNDLE_ID'] == 'com.kenk.tagtag.staging'
        and firebase['API_KEY'] == staging['firebaseApiKey']
        and firebase['CLIENT_ID'] == client
        and firebase['REVERSED_CLIENT_ID'] == staging['googleReversedClientId']
    )
except (OSError, ValueError, KeyError, TypeError):
    valid = False
if not valid:
    sys.exit('Staging configuration and downloaded Firebase iOS plist must match the approved isolated project.')
PY

review_project=$(mktemp -d /private/tmp/tagtag-ios-staging.XXXXXX)
copy_target=
cleanup() {
    local status=$?
    trap - EXIT
    if [[ $status -ne 0 ]]; then
        mkdir -p "$source_project/Build"
        local log_target
        log_target=$(mktemp -d "$source_project/Build/staging-logs.XXXXXX")
        for name in prepare.log export.log; do
            if [[ -f $review_project/$name ]]; then cp "$review_project/$name" "$log_target/$name"; fi
        done
        echo "Staging logs: $log_target" >&2
    fi
    rm -rf "$review_project"
    if [[ -n $copy_target && -d $copy_target ]]; then rm -rf "$copy_target"; fi
    exit "$status"
}
trap cleanup EXIT

for directory in Assets Packages ProjectSettings; do
    mkdir "$review_project/$directory"
    rsync -a \
        --exclude='InitTestScene*.unity' \
        --exclude='InitTestScene*.unity.meta' \
        "$source_project/$directory/" "$review_project/$directory/"
done
cp "$review_project/Assets/Resources/Tagtag/ServiceConfiguration.json" \
    "$review_project/.tagtag-production-service-configuration.json"
touch "$review_project/.tagtag-staging-build"

"$unity_editor" -batchmode -nographics -quit \
    -projectPath "$review_project" -buildTarget iOS \
    -executeMethod BuildIos.PrepareStaging \
    -logFile "$review_project/prepare.log"

"$unity_editor" -batchmode -nographics -quit \
    -projectPath "$review_project" -buildTarget iOS \
    -executeMethod BuildIos.BuildStaging \
    -logFile "$review_project/export.log"

if ! grep -Fq 'iOS Xcode project exported to' "$review_project/export.log" ||
    [[ ! -f $review_project/Build/iOS-staging/Unity-iPhone.xcodeproj/project.pbxproj ]]; then
    echo "Staging export did not complete." >&2
    exit 1
fi

mkdir -p "$source_project/Build"
copy_target=$(mktemp -d "$source_project/Build/.ios-staging.XXXXXX")
rsync -a "$review_project/Build/iOS-staging/" "$copy_target/"
cp "$review_project/prepare.log" "$copy_target/prepare.log"
cp "$review_project/export.log" "$copy_target/export.log"
mv "$copy_target" "$source_project/Build/iOS-staging"
copy_target=
echo "Staging Xcode project: $source_project/Build/iOS-staging"
