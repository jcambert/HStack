#!/usr/bin/env bash
set -euo pipefail

echo '[M8] Verify pinned Aspire CLI'
aspire --version | grep -F '13.6.0'

output_file="$(mktemp)"
home_dir="$(mktemp -d)"
project_dir="$(mktemp -d)"
on_exit() {
  result=$?
  if (( result != 0 )); then
    echo '[M8] Failed command output:' >&2
    cat "$output_file" >&2 || true
    echo '[M8] Aspire CLI and AppHost startup diagnostics:' >&2
    if [[ -d "$HOME/.aspire/logs" ]]; then
      find "$HOME/.aspire/logs" -maxdepth 1 -type f -name '*.log' -mmin -20 -print |
        while IFS= read -r logfile; do
          echo "[M8] Log: $(basename "$logfile")" >&2
          tail -n 100 "$logfile" >&2 || true
        done
    fi
    echo '[M8] Docker containers:' >&2
    docker ps -a >&2 || true
    echo '[M8] Workspace container status:' >&2
    docker ps -a --filter 'name=hstack-aspire-demo' >&2 || true
    docker logs --tail 80 hstack-aspire-demo-workspace >&2 || true
    echo '[M8] OpenViking container status and startup errors:' >&2
    docker ps -a --filter 'name=hstack-memory-openviking' >&2 || true
    docker logs --tail 60 hstack-memory-openviking >&2 || true
  fi
  if [[ -d "$home_dir" ]]; then
    run_hstack down aspire-demo --orchestrator aspire >/dev/null 2>&1 || true
    run_hstack memory stop >/dev/null 2>&1 || true
  fi
  rm -f "$output_file"
  rm -rf "$home_dir" "$project_dir"
}
trap on_exit EXIT

run_hstack() {
  HSTACK_HOME="$home_dir" dotnet run \
    --project src/HermesStack.Cli/HermesStack.Cli.csproj \
    -c Release \
    --no-build \
    -- "$@"
}

echo '[M8] Initialize Aspire as the default orchestrator'
# Earlier in the same required Linux CI job, M3 builds and checks the real,
# fully pinned Claude/Codex/Hermes/OpenCode image. Do not substitute a base
# image or download the entire toolchain again.
run_hstack init --orchestrator aspire --ci-reuse-verified-full-image >"$output_file"
grep -q 'Aspire' "$output_file"
grep -q 'default: aspire' "$home_dir/config/hstack.yaml"
grep -q 'exposeToLan: false' "$home_dir/config/hstack.yaml"

echo '[M8] Register project and validate shared deployment plan'
run_hstack project add aspire-demo "$project_dir" --quiet
run_hstack orchestrator status --project aspire-demo >"$output_file"
grep -q 'aspire' "$output_file"
run_hstack plan aspire-demo --orchestrator aspire >"$output_file"
grep -q 'no-new-privileges' "$output_file"
grep -q 'deployment.json' "$output_file"

echo '[M8] Enable project-scoped OpenViking: startup and private non-root mounts are required'
run_hstack memory enable aspire-demo >"$output_file"

echo '[M8] Start real workspace through Aspire'
run_hstack up aspire-demo --orchestrator aspire >"$output_file"
run_hstack status aspire-demo --json --orchestrator aspire >"$output_file"
grep -q '"workspace":"Running"' "$output_file"
grep -q '"orchestrator":"aspire"' "$output_file"

echo '[M8] Enforce live OCI workspace isolation (not only deployment-plan intent)'
workspace='hstack-aspire-demo-workspace'
test "$(docker inspect --format '{{.HostConfig.Privileged}}' "$workspace")" = false
test "$(docker inspect --format '{{.HostConfig.ReadonlyRootfs}}' "$workspace")" = true
test "$(docker exec "$workspace" id -u)" != 0
docker exec "$workspace" test ! -S /var/run/docker.sock
docker inspect --format '{{json .HostConfig.CapDrop}}' "$workspace" | jq -e 'index("ALL")' >/dev/null
docker inspect --format '{{json .HostConfig.SecurityOpt}}' "$workspace" | jq -e 'any(.[]; startswith("no-new-privileges"))' >/dev/null
test "$(docker inspect --format '{{.HostConfig.NetworkMode}}' "$workspace")" != host
docker inspect --format '{{json .HostConfig.PortBindings}}' "$workspace" |
  jq -e 'all(.[]?[]?; .HostIp == "127.0.0.1")' >/dev/null

echo '[M8] Verify full agent image, not a CI base-image substitution'
for agent in claude codex hermes opencode; do
  docker exec "$workspace" "$agent" --version
done
test "$(docker image inspect hstack/workspace-full:0.8.0 --format '{{index .Config.Labels "io.hstack.kind"}}')" = workspace-full

echo '[M8] Verify OpenViking operates non-root with owner-private host credentials'
context='hstack-memory-openviking'
test "$(docker exec "$context" id -u)" != 0
docker exec "$context" test -r /run/secrets/openviking_root_api_key
docker exec "$context" test -r /app/.openviking/ov.conf
test "$(stat -c '%a' "$home_dir/runtime/openviking/root-api-key")" = 600
test "$(stat -c '%a' "$home_dir/data/openviking/ov.conf")" = 600
test "$(docker inspect --format '{{.HostConfig.Privileged}}' "$context")" = false
run_hstack memory doctor aspire-demo >"$output_file"

echo '[M8] Validate Aspire dashboard, resources and agent state'
run_hstack aspire status aspire-demo >"$output_file"
grep -q 'Running' "$output_file"
run_hstack aspire dashboard aspire-demo >"$output_file"
grep -q 'Aspire Dashboard:' "$output_file"
run_hstack aspire inspect aspire-demo >"$output_file"
grep -q 'Security parity' "$output_file"
grep -q 'yes' "$output_file"

run_hstack agent status --project aspire-demo >"$output_file"
grep -q 'Claude Code' "$output_file"
grep -q 'Codex' "$output_file"
grep -q 'Hermes' "$output_file"
grep -q 'OpenCode' "$output_file"

echo '[M8] Verify backend-neutral sessions, token/context and security flows'
run_hstack session agents aspire-demo >"$output_file"
run_hstack token status aspire-demo >"$output_file"
run_hstack memory status aspire-demo >"$output_file"
run_hstack security inspect aspire-demo --orchestrator aspire >"$output_file"
grep -q 'Docker socket.*absent' "$output_file"
grep -q 'Privileged.*no' "$output_file"
grep -q '127.0.0.1' "$home_dir/config/hstack.yaml" || true

run_hstack doctor aspire-demo --orchestrator aspire >"$output_file"
run_hstack doctor --aspire >"$output_file"
grep -q '13.6.0' "$output_file"

echo '[M8] Stop and switch orchestration only while stopped'
run_hstack down aspire-demo --orchestrator aspire >"$output_file"
run_hstack orchestrator set compose --project aspire-demo >"$output_file"
grep -q 'compose' "$output_file"
run_hstack orchestrator set aspire --project aspire-demo >"$output_file"
grep -q 'aspire' "$output_file"

run_hstack memory stop >"$output_file" || true
run_hstack project remove aspire-demo --yes >"$output_file"

echo '[M8] Aspire Experience end-to-end validation passed'
