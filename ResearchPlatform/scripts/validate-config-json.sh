#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CFG_DIR="$ROOT_DIR/src/Composition/ResearchPlatform.App"

check_env() {
  local env="$1"
  local merged

  merged=$(jq -s '.[0] * .[1]' "$CFG_DIR/appsettings.json" "$CFG_DIR/appsettings.${env}.json")

  jq -e '
    .Runtime.LogLevel | type == "string" and length > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .DataIngestion.Provider | type == "string" and length > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .DataIngestion.Universes | type == "array" and length > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .DataIngestion.RequestTimeoutSeconds | type == "number" and . > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .DataWarehouse.ConnectionString | type == "string" and length > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .DataWarehouse.MaxBatchSize | type == "number" and . > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .Backtest.InitialCapital | type == "number" and . > 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .Backtest.CommissionBps | type == "number" and . >= 0
  ' >/dev/null <<<"$merged"

  jq -e '
    .Backtest.SlippageBps | type == "number" and . >= 0
  ' >/dev/null <<<"$merged"

  echo "JSON config validated for ${env}"
}

for env in Development Test Production; do
  check_env "$env"
done

echo "All JSON environment configs validated successfully."
