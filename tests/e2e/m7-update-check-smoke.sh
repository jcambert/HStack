#!/usr/bin/env bash
set -euo pipefail

echo '[M7] Verify update check CLI against the managed toolchain channel'
output_file="$(mktemp)"
home_dir="$(mktemp -d)"
trap 'rm -f "$output_file"; rm -rf "$home_dir"' EXIT

HSTACK_HOME="$home_dir" dotnet run   --project src/HermesStack.Cli/HermesStack.Cli.csproj   -c Release   --no-build   -- update check >"$output_file"

grep -q 'HermesStack' "$output_file"
grep -q 'Workspace image' "$output_file"
grep -q 'Claude Code' "$output_file"
grep -q 'Codex' "$output_file"
grep -q 'OpenViking' "$output_file"
grep -q 'Managed update source:' "$output_file"
grep -q 'Managed components match the update channel.' "$output_file"

echo '[M7] Update check smoke checks passed'
