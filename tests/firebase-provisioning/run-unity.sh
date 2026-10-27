#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${UNITY_EDITOR:-}" || ! -x "$UNITY_EDITOR" ]]; then
  echo "Set UNITY_EDITOR to the Unity Editor executable before running this test." >&2
  exit 2
fi

if [[ "${FIREBASE_AUTH_EMULATOR_HOST:-}" != "127.0.0.1:9099" ||
      "${FIRESTORE_EMULATOR_HOST:-}" != "127.0.0.1:8080" ]]; then
  echo "Refusing provisioning test without both expected local Firebase emulators." >&2
  exit 2
fi

result_file="${TMPDIR:-/tmp}/HABIMON-f15-provisioning-playmode.xml"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter HABIMON.Scene.PlayMode.Tests.FirebaseProvisioningEmulatorTests.ExistingAuthUserIsProvisionedOnLogin \
  -testResults "$result_file"
