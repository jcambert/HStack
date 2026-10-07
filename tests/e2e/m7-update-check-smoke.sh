#!/usr/bin/env bash
set -euo pipefail

echo '[M7] Verify operations CLI'
output_file="$(mktemp)"
home_dir="$(mktemp -d)"
archive_dir="$(mktemp -d)"
project_dir="$(mktemp -d)"
trap 'rm -f "$output_file"; rm -rf "$home_dir" "$archive_dir" "$project_dir"' EXIT

run_hstack() {
  HSTACK_HOME="$home_dir" dotnet run \
    --project src/HermesStack.Cli/HermesStack.Cli.csproj \
    -c Release \
    --no-build \
    -- "$@"
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

echo '[M7] Non-interactive configuration'
run_hstack config validate --json >"$output_file"
grep -q '"valid":true' "$output_file"

run_hstack project list --json >"$output_file"
grep -Fxq '[]' "$output_file"

echo '[M7] Safe project port lifecycle'
run_hstack project add demo "$project_dir" --quiet
run_hstack port add demo 5000 >"$output_file"
grep -q '127.0.0.1:5000:5000' "$output_file"
run_hstack port list demo >"$output_file"
grep -q '127.0.0.1' "$output_file"
run_hstack port remove demo 5000 >"$output_file"

echo '[M7] Backup and portable export'
run_hstack backup --config-only --output "$archive_dir/backup.zip" >"$output_file"
test -s "$archive_dir/backup.zip"
grep -q 'Backup created:' "$output_file"

run_hstack export "$archive_dir/environment.hstack" >"$output_file"
test -s "$archive_dir/environment.hstack"
grep -q 'Portable environment exported:' "$output_file"

echo '[M7] Encrypted portable secrets'
export HSTACK_TEST_SECRET='m7-portable-secret-value'
export HSTACK_TEST_EXPORT_PASSWORD='correct-horse-battery-staple'
run_hstack secret set API_KEY \
  --project demo \
  --agents claude \
  --from-env HSTACK_TEST_SECRET >"$output_file"
run_hstack export "$archive_dir/environment-with-secrets.hstack" \
  --include-secrets \
  --passphrase-env HSTACK_TEST_EXPORT_PASSWORD >"$output_file"
unzip -l "$archive_dir/environment-with-secrets.hstack" | grep -q 'secrets.enc'
if unzip -p "$archive_dir/environment-with-secrets.hstack" secrets.enc | grep -q 'm7-portable-secret-value'; then
  echo 'plaintext secret leaked into encrypted portable package' >&2
  exit 1
fi

run_hstack project remove demo --yes >"$output_file"

echo '[M7] Spectre literal bracket help renders safely'
run_hstack help >"$output_file"
grep -Fq 'hstack status [project] [--json] [--quiet]' "$output_file"
grep -Fq 'hstack update apply --yes' "$output_file"
grep -Fq 'hstack port add <project> <containerPort> [--host <port>]' "$output_file"
grep -Fq 'hstack export <environment.hstack> [--include-memory] [--include-secrets --passphrase-env <ENV>]' "$output_file"

echo '[M7] Operations smoke checks passed'
