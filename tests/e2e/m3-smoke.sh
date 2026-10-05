#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
tmp_root="$(mktemp -d)"
export HSTACK_HOME="$tmp_root/hstack-home"
export NO_COLOR=1

project_a="$tmp_root/ProjectA"
project_b="$tmp_root/ProjectB"
container="hstack-project-a-workspace"

run_hstack() {
  dotnet run --project "$repo_root/src/HermesStack.Cli/HermesStack.Cli.csproj" \
    --configuration Release --no-build -- "$@"
}

cleanup() {
  run_hstack down project-a >/dev/null 2>&1 || true
  run_hstack down project-b >/dev/null 2>&1 || true
  rm -rf "$tmp_root"
}
trap cleanup EXIT

mkdir -p "$project_a" "$project_b"
printf 'secret-a\n' > "$project_a/secret-a.txt"
printf 'secret-b\n' > "$project_b/secret-b.txt"

openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 1 \
  -subj "/CN=HermesStack M2 Test CA" \
  -keyout "$tmp_root/ca.key" -out "$tmp_root/ca.crt" >/dev/null 2>&1

run_hstack init
run_hstack cert add "$tmp_root/ca.crt"
run_hstack project add project-a "$project_a"
run_hstack project add project-b "$project_b"
run_hstack status project-b >/dev/null
run_hstack up project-a

# Preserve all M1 isolation and runtime-security assertions.
test "$(docker exec "$container" cat /workspace/secret-a.txt)" = "secret-a"
docker exec "$container" test -w /workspace
docker exec "$container" test ! -e "$project_b/secret-b.txt"
docker exec "$container" test ! -e /home/hstack/.ssh
docker exec "$container" test ! -S /var/run/docker.sock
test "$(docker exec "$container" id -u)" != "0"

inspect="$(docker inspect --format '{{.HostConfig.Privileged}}|{{.HostConfig.ReadonlyRootfs}}|{{json .HostConfig.CapDrop}}|{{json .HostConfig.SecurityOpt}}|{{.HostConfig.NetworkMode}}|{{.Config.User}}' "$container")"
[[ "$inspect" == false\|true\|* ]]
[[ "$inspect" == *'["ALL"]'* ]]
[[ "$inspect" == *'no-new-privileges:true'* ]]
[[ "$inspect" != *'|host|'* ]]
[[ "$inspect" == *'|hstack' ]]

# M2 agents plus the M3 session tooling are present at exact pins.
docker exec "$container" herdr --version | grep -F "0.9.3"
docker exec "$container" tmux -V | grep -F "tmux"

# All four coding agents are present at the exact M2 pins.
docker exec "$container" claude --version | grep -F "2.1.289"
docker exec "$container" codex --version | grep -F "0.160.0"
docker exec "$container" hermes --version | grep -F "0.21.5"
docker exec "$container" opencode --version | grep -F "1.18.34"

# Public CLI aliases and registry inspection execute inside the workspace.
run_hstack claude project-a -- --version
run_hstack codex project-a -- --version
run_hstack hermes project-a -- --version
run_hstack opencode project-a -- --version

# M3 initializes one project-owned Herdr session and installs the official
# integrations into the already isolated M2 agent state.
run_hstack session init project-a
test "$(docker exec "$container" printenv HERDR_SESSION)" = "hstack-project-a"
test -f "$HSTACK_HOME/data/projects/project-a/claude/hooks/herdr-agent-state.sh"
test -f "$HSTACK_HOME/data/projects/project-a/codex/herdr-agent-state.sh"
test -d "$HSTACK_HOME/data/projects/project-a/hermes/plugins/herdr-agent-state"
test -f "$HSTACK_HOME/data/projects/project-a/opencode/config/plugins/herdr-agent-state.js"

herdr_state="$HSTACK_HOME/data/projects/project-a/home/.config/herdr/sessions/hstack-project-a/session.json"
for _ in $(seq 1 40); do
  test -s "$herdr_state" && break
  sleep 0.1
done
test -s "$herdr_state"

workspace_before="$(docker exec "$container" herdr workspace list | jq -r '.result.workspaces[] | select(.label=="hstack:project-a") | .workspace_id')"
test -n "$workspace_before"

agent_list="$(run_hstack agent list --project project-a)"
grep -F "2.1.289" <<<"$agent_list" >/dev/null
grep -F "0.160.0" <<<"$agent_list" >/dev/null
grep -F "0.21.5" <<<"$agent_list" >/dev/null
grep -F "1.18.34" <<<"$agent_list" >/dev/null

# Agent state and auth homes are nested, explicit project-scoped mounts.
mounts="$(docker inspect --format '{{range .Mounts}}{{println .Source "->" .Destination}}{{end}}' "$container")"
grep -F "$HSTACK_HOME/data/projects/project-a/claude -> /home/hstack/.claude" <<<"$mounts" >/dev/null
grep -F "$HSTACK_HOME/data/projects/project-a/codex -> /home/hstack/.codex" <<<"$mounts" >/dev/null
grep -F "$HSTACK_HOME/data/projects/project-a/hermes -> /home/hstack/.hermes" <<<"$mounts" >/dev/null
grep -F "$HSTACK_HOME/data/projects/project-a/opencode/config -> /home/hstack/.config/opencode" <<<"$mounts" >/dev/null
grep -F "$HSTACK_HOME/data/projects/project-a/opencode/data -> /home/hstack/.local/share/opencode" <<<"$mounts" >/dev/null
test -d "$HSTACK_HOME/data/projects/project-a/home/.local/state"
test -d "$HSTACK_HOME/data/projects/project-a/home/.cache"

if grep -F "$HSTACK_HOME/data/projects/project-b" <<<"$mounts" >/dev/null; then
  echo "ProjectB agent state leaked into ProjectA" >&2
  exit 1
fi

# Prove writes land only in ProjectA's dedicated state.
docker exec "$container" sh -c 'printf claude > "$CLAUDE_CONFIG_DIR/hstack-marker"'
docker exec "$container" sh -c 'printf codex > "$CODEX_HOME/hstack-marker"'
docker exec "$container" sh -c 'printf hermes > "$HERMES_HOME/hstack-marker"'
docker exec "$container" sh -c 'printf opencode > "$HOME/.local/share/opencode/hstack-marker"'

test "$(cat "$HSTACK_HOME/data/projects/project-a/claude/hstack-marker")" = "claude"
test "$(cat "$HSTACK_HOME/data/projects/project-a/codex/hstack-marker")" = "codex"
test "$(cat "$HSTACK_HOME/data/projects/project-a/hermes/hstack-marker")" = "hermes"
test "$(cat "$HSTACK_HOME/data/projects/project-a/opencode/data/hstack-marker")" = "opencode"
test ! -e "$HSTACK_HOME/data/projects/project-b/claude/hstack-marker"
test ! -e "$HSTACK_HOME/data/projects/project-b/codex/hstack-marker"
test ! -e "$HSTACK_HOME/data/projects/project-b/hermes/hstack-marker"
test ! -e "$HSTACK_HOME/data/projects/project-b/opencode/data/hstack-marker"

test "$(docker exec "$container" printenv CLAUDE_CONFIG_DIR)" = "/home/hstack/.claude"
test "$(docker exec "$container" printenv CODEX_HOME)" = "/home/hstack/.codex"
test "$(docker exec "$container" printenv HERMES_HOME)" = "/home/hstack/.hermes"
test "$(docker exec "$container" printenv TERMINAL_ENV)" = "local"
test "$(docker exec "$container" printenv OPENCODE_DISABLE_AUTOUPDATE)" = "1"

grep -F 'cli_auth_credentials_store = "file"' "$HSTACK_HOME/data/projects/project-a/codex/config.toml" >/dev/null
grep -F 'backend: local' "$HSTACK_HOME/data/projects/project-a/hermes/config.yaml" >/dev/null
grep -F '"autoupdate": false' "$HSTACK_HOME/data/projects/project-a/opencode/config/opencode.json" >/dev/null

# Recreate the Docker workspace: Herdr restores its project workspace from the
# persisted session snapshot instead of creating a duplicate.
run_hstack down project-a
run_hstack up project-a
run_hstack session init project-a >/dev/null
workspace_after="$(docker exec "$container" herdr workspace list | jq -r '.result.workspaces[] | select(.label=="hstack:project-a") | .workspace_id')"
test "$workspace_after" = "$workspace_before"
test "$(docker exec "$container" herdr workspace list | jq '[.result.workspaces[] | select(.label=="hstack:project-a")] | length')" = "1"

# tmux remains a supported fallback multiplexer inside the same sandbox.
docker exec "$container" tmux new-session -d -s m3-smoke 'sleep 60'
docker exec "$container" tmux has-session -t m3-smoke
docker exec "$container" tmux kill-session -t m3-smoke

run_hstack down project-a
if docker inspect "$container" >/dev/null 2>&1; then
  echo "Workspace container still exists after hstack down" >&2
  exit 1
fi

echo "M3 Sessions end-to-end validation passed."
