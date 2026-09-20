#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
APK_PATH="${APK_PATH:-$REPO_ROOT/Unity/Build/Android/SpatialDebugger.apk}"
PACKAGE_ID="${PACKAGE_ID:-com.htn2026.spatialdebugger}"
OLLAMA_URL="${OLLAMA_URL:-http://127.0.0.1:11434}"
OLLAMA_MODEL="${OLLAMA_MODEL:-moondream}"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

need() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

need adb
need curl

if [[ -n "${ANDROID_SERIAL:-}" ]]; then
  DEVICE_SERIAL="$ANDROID_SERIAL"
  DEVICE_STATE="$(adb -s "$DEVICE_SERIAL" get-state 2>/dev/null || true)"
  [[ "$DEVICE_STATE" == "device" ]] || fail "ANDROID_SERIAL=$DEVICE_SERIAL is not ready."
else
  DEVICE_COUNT="$(adb devices | awk 'NR > 1 && $2 == "device" { count++ } END { print count + 0 }')"
  if [[ "$DEVICE_COUNT" -ne 1 ]]; then
    adb devices >&2
    fail "Expected exactly one authorized device; found $DEVICE_COUNT. Set ANDROID_SERIAL if needed."
  fi
  DEVICE_SERIAL="$(adb devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
fi

ADB=(adb -s "$DEVICE_SERIAL")

printf 'Quest: %s\n' "$DEVICE_SERIAL"
printf 'Checking Ollama at %s...\n' "$OLLAMA_URL"
MODEL_LIST="$(curl --fail --silent --show-error --max-time 3 "$OLLAMA_URL/api/tags")" || \
  fail "Ollama is not reachable. Start it with: ollama serve"

if ! printf '%s' "$MODEL_LIST" | grep -Fqi -- "$OLLAMA_MODEL"; then
  fail "Model '$OLLAMA_MODEL' is not installed. Run: ollama pull $OLLAMA_MODEL"
fi

[[ -f "$APK_PATH" ]] || fail "APK not found: $APK_PATH. Build it in Unity first."

printf 'Configuring USB port reverse...\n'
"${ADB[@]}" reverse tcp:11434 tcp:11434

printf 'Installing %s...\n' "$APK_PATH"
"${ADB[@]}" install -r "$APK_PATH"

printf 'Clearing old logcat and launching %s...\n' "$PACKAGE_ID"
"${ADB[@]}" logcat -c
"${ADB[@]}" shell am force-stop "$PACKAGE_ID"
"${ADB[@]}" shell am start -n "$PACKAGE_ID/com.unity3d.player.UnityPlayerGameActivity"

printf '\nDemo launched. In another terminal run:\n  %s/demo-logs.sh\n' "$SCRIPT_DIR"
printf 'Wait for [camera] READY before the first recognition pinch.\n'
