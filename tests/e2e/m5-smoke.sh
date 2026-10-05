#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
tmp_root="$(mktemp -d)"
export HSTACK_HOME="$tmp_root/hstack-home"
export NO_COLOR=1

project="$tmp_root/ProjectA"
container="hstack-project-a-workspace"

run_hstack() {
  dotnet run --project "$repo_root/src/HermesStack.Cli/HermesStack.Cli.csproj" \
    --configuration Release --no-build -- "$@"
}

cleanup() {
  run_hstack down project-a >/dev/null 2>&1 || true
  rm -rf "$tmp_root"
}
trap cleanup EXIT

mkdir -p "$project"
printf 'm5\n' > "$project/marker.txt"

# M3 builds the current pinned image and M4 already validates M1-M4 behavior.
docker image inspect hstack/workspace-full:0.5.0 >/dev/null

run_hstack project add project-a "$project"
run_hstack up project-a

# Supply-chain pins are present in the actual workspace.
docker exec "$container" rtk --version | grep -F "0.51.0"
test "$(docker exec "$container" node -p "require('/opt/caveman/package.json').version")" = "2.7.0"

providers="$(run_hstack token providers)"
grep -F "rtk" <<<"$providers" >/dev/null
grep -F "0.51.0" <<<"$providers" >/dev/null
grep -F "caveman" <<<"$providers" >/dev/null
grep -F "2.7.0" <<<"$providers" >/dev/null

# Balanced is the default safe integration path: RTK only.
run_hstack token enable project-a \
  --provider rtk \
  --profile balanced \
  --agents claude,codex,hermes,opencode

status="$(run_hstack token status project-a)"
grep -F "balanced" <<<"$status" >/dev/null
grep -F "rtk" <<<"$status" >/dev/null

# Privacy policy: RTK's raw recall is disabled, telemetry is disabled, and its
# command-history DB is forced into the workspace /tmp tmpfs rather than project
# persistent state.
test "$(docker exec "$container" printenv RTK_DB_PATH)" = "/tmp/hstack-rtk-tracking.db"
test "$(docker exec "$container" printenv RTK_TELEMETRY_DISABLED)" = "1"
test "$(docker exec "$container" printenv RTK_RECALL)" = "0"
docker exec "$container" rtk config recall | grep -Fi "disabled"

# Verify the exact upstream integration artifacts instead of guessing by
# filename. These paths are RTK v0.51.0's documented global install targets.
assert_in_container() {
  local path="$1"
  if ! docker exec "$container" test -e "$path"; then
    echo "Expected RTK integration artifact is missing: $path" >&2
    docker exec "$container" sh -c 'find "$HOME" -maxdepth 5 -type f | sort' >&2
    exit 1
  fi
}
assert_in_container /home/hstack/.claude/RTK.md
assert_in_container /home/hstack/.codex/hooks.json
assert_in_container /home/hstack/.hermes/plugins/rtk-rewrite/plugin.yaml
assert_in_container /home/hstack/.config/opencode/plugins/rtk.ts

# Generate real RTK aggregate data. This DB is intentionally ephemeral.
docker exec "$container" rtk proxy ls /usr/bin >/dev/null
if ! docker exec "$container" test -s /tmp/hstack-rtk-tracking.db; then
  echo "RTK tracking database was not created at the enforced tmpfs path" >&2
  exit 1
fi

gain="$(run_hstack token gain project-a)"
grep -F "Estimated" <<<"$gain" >/dev/null
grep -F "not LLM billing" <<<"$gain" >/dev/null

metric_file="$HSTACK_HOME/data/projects/project-a/metrics/token-gain.jsonl"
test -s "$metric_file"
grep -F '"Evidence":1' "$metric_file" >/dev/null
if grep -E -i 'prompt|response|source.?code|secret.?value' "$metric_file" >/dev/null; then
  echo "Sanitized token metric store contains forbidden content fields" >&2
  cat "$metric_file" >&2
  exit 1
fi

# Caveman is semantic compression: balanced mode must refuse it, and stacking
# with RTK requires an explicit potentially-lossy override.
if run_hstack token enable project-a --provider caveman --profile balanced --agents hermes >/dev/null 2>&1; then
  echo "Caveman incorrectly enabled under balanced profile" >&2
  exit 1
fi

if run_hstack token enable project-a --provider caveman --profile aggressive --agents hermes >/dev/null 2>&1; then
  echo "Potentially-lossy RTK+Caveman stack did not require explicit consent" >&2
  exit 1
fi

run_hstack token enable project-a \
  --provider caveman \
  --profile aggressive \
  --agents hermes \
  --allow-lossy-stack

docker exec "$container" sh -c 'find "$HERMES_HOME" -iname "*caveman*" -print | grep -q .'

security="$(run_hstack security inspect project-a)"
grep -F "Token optimizer hooks" <<<"$security" >/dev/null
grep -F "rtk" <<<"$security" >/dev/null
grep -F "caveman" <<<"$security" >/dev/null
grep -F "RTK recall disabled" <<<"$security" >/dev/null

doctor="$(run_hstack doctor project-a --tokens)"
grep -F "RTK" <<<"$doctor" >/dev/null
grep -F "Caveman" <<<"$doctor" >/dev/null
grep -F "PASS" <<<"$doctor" >/dev/null

token_doctor="$(run_hstack token doctor project-a)"
grep -F "Ready" <<<"$token_doctor" >/dev/null
grep -F "0.51.0" <<<"$token_doctor" >/dev/null
grep -F "2.7.0" <<<"$token_doctor" >/dev/null

# No upstream raw-output recall DB may become durable in project HOME.
if find "$HSTACK_HOME/data/projects/project-a" -type f \( -name 'recall.db' -o -name 'tracking.db' -o -name 'history.db' \) -print | grep -q .; then
  echo "Raw RTK history/recall database leaked into persistent project state" >&2
  find "$HSTACK_HOME/data/projects/project-a" -type f -name '*.db' -print >&2
  exit 1
fi

run_hstack down project-a
echo "M5 Token Efficiency end-to-end validation passed."
