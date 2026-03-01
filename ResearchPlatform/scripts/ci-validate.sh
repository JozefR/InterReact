#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "[1/7] Restore"
dotnet restore ResearchPlatform.sln

echo "[2/7] Build (warnings as errors)"
dotnet build ResearchPlatform.sln --no-restore -warnaserror

echo "[3/7] Format/lint"
dotnet format ResearchPlatform.sln --verify-no-changes --no-restore

echo "[4/7] Module boundary check"
./scripts/check-module-boundaries.sh

echo "[5/7] Config schema validation"
./scripts/validate-config-json.sh

echo "[6/7] Runtime config validation"
NO_BUILD=1 ./scripts/validate-config.sh

echo "[7/7] Tests"
dotnet test ResearchPlatform.sln --no-build --verbosity normal

echo "CI validation completed successfully."
