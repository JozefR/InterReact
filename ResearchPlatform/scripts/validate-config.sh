#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PROJECT="$ROOT_DIR/src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj"

for env in Development Test Production; do
  echo "Validating config for $env"
  RP_ENVIRONMENT="$env" dotnet run --project "$APP_PROJECT" -- --validate-config
  echo
 done

echo "All environments validated successfully."
