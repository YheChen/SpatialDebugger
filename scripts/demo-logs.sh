#!/usr/bin/env bash

set -euo pipefail

need() {
  command -v "$1" >/dev/null 2>&1 || {
    printf 'ERROR: Required command not found: %s\n' "$1" >&2
    exit 1
  }
}

need adb

if [[ -n "${ANDROID_SERIAL:-}" ]]; then
  DEVICE_SERIAL="$ANDROID_SERIAL"
  DEVICE_STATE="$(adb -s "$DEVICE_SERIAL" get-state 2>/dev/null || true)"
  [[ "$DEVICE_STATE" == "device" ]] || {
    printf 'ERROR: ANDROID_SERIAL=%s is not ready.\n' "$DEVICE_SERIAL" >&2
    exit 1
  }
else
  DEVICE_COUNT="$(adb devices | awk 'NR > 1 && $2 == "device" { count++ } END { print count + 0 }')"
  if [[ "$DEVICE_COUNT" -ne 1 ]]; then
    adb devices >&2
    printf 'ERROR: Expected exactly one authorized device; found %s.\n' "$DEVICE_COUNT" >&2
    exit 1
  fi
  DEVICE_SERIAL="$(adb devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
fi

printf 'Following SpatialDebugger logs on %s (Ctrl-C to stop)...\n' "$DEVICE_SERIAL"

adb -s "$DEVICE_SERIAL" logcat -v threadtime | \
  grep --line-buffered -E \
  'SpatialDebugger.*(\[camera\]|\[vision\]|placed annotation|annotation #[0-9]+|rejected action)|FATAL EXCEPTION|AndroidRuntime|NullReferenceException|MissingReferenceException|Unity.*(Exception|ERROR|Error)'
