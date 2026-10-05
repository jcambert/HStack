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
  dotnet run --project "$repo_root/src/HermesStack.Cli/HermesStack.Cli.csproj" --configuration Release --no-build -- "$@"
}

cleanup() {
  run_hstack down project-a >/dev/null 2>&1 || true
  rm -rf "$tmp_root"
}
trap cleanup EXIT

mkdir -p "$project_a" "$project_b"
printf 'secret-a\n' > "$project_a/secret-a.txt"
printf 'secret-b\n' > "$project_b/secret-b.txt"

openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 1 \
  -subj "/CN=HermesStack M1 Test CA" \
  -keyout "$tmp_root/ca.key" -out "$tmp_root/ca.crt" >/dev/null 2>&1

run_hstack init
run_hstack cert add "$tmp_root/ca.crt"
run_hstack project add project-a "$project_a"
run_hstack project list | grep -F "project-a" >/dev/null
run_hstack up project-a

test "$(docker exec "$container" cat /workspace/secret-a.txt)" = "secret-a"
docker exec "$container" test -w /workspace
docker exec "$container" test ! -e "$project_b/secret-b.txt"
docker exec "$container" test ! -e "$HOME/.ssh"
docker exec "$container" test ! -S /var/run/docker.sock
test "$(docker exec "$container" id -u)" != "0"

inspect="$(docker inspect --format '{{.HostConfig.Privileged}}|{{.HostConfig.ReadonlyRootfs}}|{{json .HostConfig.CapDrop}}|{{json .HostConfig.SecurityOpt}}|{{.HostConfig.NetworkMode}}|{{.Config.User}}' "$container")"
[[ "$inspect" == false\|true\|* ]]
[[ "$inspect" == *'["ALL"]'* ]]
[[ "$inspect" == *'no-new-privileges:true'* ]]
[[ "$inspect" != *'|host|'* ]]
[[ "$inspect" == *'|hstack' ]]

mounts="$(docker inspect --format '{{range .Mounts}}{{println .Source "->" .Destination}}{{end}}' "$container")"
grep -F "$project_a -> /workspace" <<<"$mounts" >/dev/null
if grep -F "$project_b" <<<"$mounts" >/dev/null; then
  echo "ProjectB leaked into ProjectA mounts" >&2
  exit 1
fi
if grep -Ei 'docker\.sock|docker_engine' <<<"$mounts" >/dev/null; then
  echo "Docker daemon endpoint leaked into workspace" >&2
  exit 1
fi

test "$(docker exec "$container" printenv SSL_CERT_FILE)" = "/home/hstack/.hstack/certs/ca-bundle.crt"
docker exec "$container" test -s /home/hstack/.hstack/certs/ca-bundle.crt

run_hstack status project-a | grep -F "Running" >/dev/null
run_hstack down project-a

if docker inspect "$container" >/dev/null 2>&1; then
  echo "Workspace container still exists after hstack down" >&2
  exit 1
fi

echo "M1 end-to-end validation passed."
