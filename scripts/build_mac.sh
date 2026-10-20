#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}"
UNITY_PROJECT="$ROOT_DIR/GCCD_Project"
LOG_FILE="${GCCD_BUILD_LOG:-$ROOT_DIR/build-mac.log}"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "Unity editor executable was not found: $UNITY_EDITOR" >&2
  exit 1
fi

"$UNITY_EDITOR" -batchmode -nographics -quit \
  -projectPath "$UNITY_PROJECT" \
  -executeMethod GCCD.Editor.StudySceneBuilder.GenerateAndBuildMac \
  -logFile "$LOG_FILE"

if ! grep -q 'GCCD_STUDY_MAC_BUILD_SUCCESS' "$LOG_FILE"; then
  echo "Unity did not report a successful macOS build; see $LOG_FILE" >&2
  exit 1
fi
echo "Build created at $UNITY_PROJECT/Builds/ThreatIntensityStudy.app"
