#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
UNITY_RESOURCES="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Resources/Scripting"
MONO_BIN="$UNITY_RESOURCES/MonoBleedingEdge/bin/mono"
MCS_DLL="$UNITY_RESOURCES/MonoBleedingEdge/lib/mono/4.5/mcs.exe"
mkdir -p .test-build
"$MONO_BIN" "$MCS_DLL" -langversion:latest -debug -out:.test-build/CoreTests.exe GCCD_Project/Assets/Scripts/Dts/Core/*.cs GCCD_Project/Assets/Scripts/Dts/Data/*.cs scripts/tests/CoreTests.cs
"$MONO_BIN" .test-build/CoreTests.exe
