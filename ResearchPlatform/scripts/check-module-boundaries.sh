#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

violations=0

while IFS= read -r project; do
  refs=$(rg --no-heading --line-number "ProjectReference Include=" "$project" || true)

  if [[ -n "$refs" ]] && echo "$refs" | rg -q "\\.\\.\\\\\\.\\.\\\\Modules\\\\"; then
    echo "Boundary violation in $project: module references another module"
    violations=1
  fi

  if [[ -n "$refs" ]] && ! echo "$refs" | rg -q "ResearchPlatform.Contracts"; then
    echo "Boundary violation in $project: module has non-contract project references"
    violations=1
  fi
done < <(find src/Modules -name '*.csproj' | sort)

if [[ "$violations" -ne 0 ]]; then
  exit 1
fi

echo "Boundary check passed: modules are isolated behind contracts."
