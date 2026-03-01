#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

CONTEXT="DataWarehouse.Schema.ResearchWarehouseDbContext"
PROJECT="src/Modules/DataWarehouse/DataWarehouse.csproj"
STARTUP="src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj"

if [[ ! -f ".config/dotnet-tools.json" ]]; then
  echo "Missing .config/dotnet-tools.json. Run 'dotnet new tool-manifest' and install dotnet-ef."
  exit 1
fi

dotnet tool restore

dotnet dotnet-ef database update \
  --project "$PROJECT" \
  --startup-project "$STARTUP" \
  --context "$CONTEXT"

echo "Database migration applied successfully."
