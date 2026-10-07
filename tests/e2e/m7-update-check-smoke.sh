#!/usr/bin/env bash
set -euo pipefail

echo '[M7] Verify operations CLI'
output_file="$(mktemp)"
home_dir="$(mktemp -d)"
archive_dir="$(mktemp -d)"
trap 'rm -f "$output_file"; rm -rf "$home_dir" "$archive_dir"' EXIT

run_hstack() {
  HSTACK_HOME="$home_dir" dotnet run     --project src/HermesStack.Cli/HermesStack.Cli.csproj     -c Release     --no-build     -- "$@"
}

echo '[M7] Managed update inventory and deterministic plan'
run_hstack update check >"$output_file"
grep -q 'HermesStack' "$output_file"
grep -q 'Workspace image' "$output_file"
grep -q 'OpenViking' "$output_file"
grep -q 'Managed update source:' "$output_file"

run_hstack update plan >"$output_file"
grep -q 'Back up HermesStack configuration' "$output_file"
grep -q 'Validate workspace health and agent availability' "$output_file"

echo '[M7] Non-interactive config and portability'
run_hstack config validate --json >"$output_file"
grep -q '"valid":true' "$output_file"

run_hstack project list --json >"$output_file"
grep -q '^\[\]$' "$output_file"

run_hstack backup --config-only --output "$archive_dir/backup.zip" >"$output_file"
test -s "$archive_dir/backup.zip"
grep -q 'Backup created:' "$output_file"

run_hstack export "$archive_dir/environment.hstack" >"$output_file"
test -s "$archive_dir/environment.hstack"
grep -q 'Portable environment exported:' "$output_file"

echo '[M7] Spectre literal bracket help renders safely'
run_hstack help >"$output_file"
grep -Fq 'hstack status [project] [--json] [--quiet]' "$output_file"
grep -Fq 'hstack update apply --yes' "$output_file"

echo '[M7] Operations smoke checks passed'
