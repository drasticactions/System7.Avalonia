#!/bin/sh
set -eu

script_dir="$(CDPATH='' cd "$(dirname "$0")" && pwd)"
if [ -n "${PROSCENIUM_QUALITY_GATE_RUNNER:-}" ]; then
    mkdir -p "$PROSCENIUM_QUALITY_GATE_RUNNER"
    exec dotnet run --artifacts-path "$PROSCENIUM_QUALITY_GATE_RUNNER/artifacts" \
        --project "$script_dir/CodeQualityPolicy.csproj" \
        -- "$@"
fi

exec dotnet run --project "$script_dir/CodeQualityPolicy.csproj" -- "$@"
