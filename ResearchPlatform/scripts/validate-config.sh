#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PROJECT="$ROOT_DIR/src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj"
NO_BUILD="${NO_BUILD:-0}"

for env in Development Test Production; do
  echo "Validating config for $env"
  run_args=(dotnet run --project "$APP_PROJECT")
  if [[ "$NO_BUILD" == "1" ]]; then
    run_args+=(--no-build)
  fi

  run_args+=(-- --validate-config)

  RP_ENVIRONMENT="$env" "${run_args[@]}"
  echo
 done

echo "All environments validated successfully."
