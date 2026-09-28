#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

"$repo_root/eng/code-quality/check-code-quality-policy.sh"

dotnet build "$repo_root/samples/System7.Demo.Desktop/System7.Demo.Desktop.csproj"
dotnet build "$repo_root/samples/System7.Demo.Browser/System7.Demo.Browser.csproj"
dotnet build "$repo_root/tests/System7.Oracle/System7.Oracle.csproj"
