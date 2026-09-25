#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 /private/tmp/<new-review-project>" >&2
    exit 2
fi

review_project=$1
if [[ $review_project != /private/tmp/* || -e $review_project || -L $review_project ]]; then
    echo "Choose a new project path under /private/tmp." >&2
    exit 2
fi

review_parent=$(dirname "$review_project")
if [[ ! -d $review_parent ]]; then
    echo "The review project's parent must be an existing directory under /private/tmp." >&2
    exit 2
fi
resolved_parent=$(cd "$review_parent" && pwd -P)
if [[ $resolved_parent != /private/tmp && $resolved_parent != /private/tmp/* ]]; then
    echo "The review project's parent must resolve under /private/tmp." >&2
    exit 2
fi

source_project=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)
unity_editor=${TAGTAG_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity}
if [[ ! -x $unity_editor ]]; then
    echo "Unity Editor is not executable: $unity_editor" >&2
    exit 2
fi

mkdir -m 700 "$review_project"
for directory in Assets Packages ProjectSettings; do
    mkdir "$review_project/$directory"
    rsync -a \
        --exclude='InitTestScene*.unity' \
        --exclude='InitTestScene*.unity.meta' \
        "$source_project/$directory/" "$review_project/$directory/"
done
touch "$review_project/.tagtag-simulator-review"

"$unity_editor" -batchmode -nographics -quit \
    -projectPath "$review_project" -buildTarget iOS \
    -executeMethod BuildIosSimulator.Prepare \
    -logFile "$review_project/prepare.log"
if ! grep -Fq 'tagtag simulator project prepared' "$review_project/prepare.log"; then
    echo "Simulator preparation did not complete; see $review_project/prepare.log" >&2
    exit 1
fi

"$unity_editor" -batchmode -nographics -quit \
    -projectPath "$review_project" -buildTarget iOS \
    -executeMethod BuildIosSimulator.Build \
    -logFile "$review_project/export.log"
if ! grep -Fq 'tagtag iOS Simulator Xcode project exported' "$review_project/export.log"; then
    echo "Simulator export did not complete; see $review_project/export.log" >&2
    exit 1
fi

xcodebuild \
    -project "$review_project/Build/iOSSimulator/Unity-iPhone.xcodeproj" \
    -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator \
    -destination 'generic/platform=iOS Simulator' \
    -derivedDataPath "$review_project/Build/DerivedDataSimulator" \
    CODE_SIGNING_ALLOWED=NO build \
    > "$review_project/xcodebuild.log" 2>&1 || {
        echo "Simulator Xcode build failed; see $review_project/xcodebuild.log" >&2
        exit 1
    }

echo "Simulator build: $review_project/Build/DerivedDataSimulator/Build/Products/Debug-iphonesimulator/tagtag.app"
echo "Logs: $review_project/prepare.log, $review_project/export.log, $review_project/xcodebuild.log"
