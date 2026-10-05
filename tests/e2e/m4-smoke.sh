#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
tmp_root="$(mktemp -d)"
export HSTACK_HOME="$tmp_root/hstack-home"
export NO_COLOR=1

project_a="$tmp_root/ProjectA"
project_b="$tmp_root/ProjectB"
container="hstack-project-a-workspace"
secret_value="m4-secret-$(date +%s)-$RANDOM"
export HSTACK_M4_SECRET="$secret_value"

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

# The M3 gate that runs immediately before this one builds the pinned 0.5.0
# workspace image. Keep this gate focused on M4 behavior.
docker image inspect hstack/workspace-full:0.5.0 >/dev/null

run_hstack project add project-a "$project_a"
run_hstack project add project-b "$project_b"

# Proxy configuration is normalized, stored without credentials, and injected
# in both uppercase/lowercase forms into the workspace service.
run_hstack proxy set \
  --http http://proxy.invalid:8080 \
  --https https://proxy.invalid:8443 \
  --no-proxy corp.internal
proxy_show="$(run_hstack proxy show)"
grep -F "proxy.invalid" <<<"$proxy_show" >/dev/null
grep -F "host.docker.internal" <<<"$proxy_show" >/dev/null

# Secret values enter through host environment only. YAML stores the policy,
# while the encrypted local store keeps the value out of config/Compose/logs.
run_hstack secret set OPENAI_API_KEY \
  --project project-a \
  --agents codex \
  --from-env HSTACK_M4_SECRET

secret_list="$(run_hstack secret list --project project-a)"
grep -F "OPENAI_API_KEY" <<<"$secret_list" >/dev/null
grep -F "codex" <<<"$secret_list" >/dev/null
if grep -F "$secret_value" <<<"$secret_list" >/dev/null; then
  echo "Secret value leaked through secret list" >&2
  exit 1
fi

if grep -R -F "$secret_value" "$HSTACK_HOME/config" >/dev/null 2>&1; then
  echo "Secret value leaked into YAML configuration" >&2
  exit 1
fi

run_hstack up project-a

test "$(docker exec "$container" printenv HTTP_PROXY)" = "http://proxy.invalid:8080"
test "$(docker exec "$container" printenv http_proxy)" = "http://proxy.invalid:8080"
test "$(docker exec "$container" printenv HTTPS_PROXY)" = "https://proxy.invalid:8443"
test "$(docker exec "$container" printenv https_proxy)" = "https://proxy.invalid:8443"
no_proxy="$(docker exec "$container" printenv NO_PROXY)"
grep -F "localhost" <<<"$no_proxy" >/dev/null
grep -F "127.0.0.1" <<<"$no_proxy" >/dev/null
grep -F "::1" <<<"$no_proxy" >/dev/null
grep -F "host.docker.internal" <<<"$no_proxy" >/dev/null
grep -F "corp.internal" <<<"$no_proxy" >/dev/null

compose_override="$HSTACK_HOME/runtime/projects/project-a/compose.override.yaml"
test -s "$compose_override"
if grep -F "$secret_value" "$compose_override" >/dev/null; then
  echo "Secret value leaked into Compose override" >&2
  exit 1
fi
if grep -F "OPENAI_API_KEY" "$compose_override" >/dev/null; then
  echo "Secret name was injected into the long-lived workspace environment" >&2
  exit 1
fi

# Existing M1 isolation remains intact after adding proxy and secret policy.
test "$(docker exec "$container" cat /workspace/secret-a.txt)" = "secret-a"
docker exec "$container" test ! -e "$project_b/secret-b.txt"
docker exec "$container" test ! -S /var/run/docker.sock
test "$(docker exec "$container" id -u)" != "0"

security="$(run_hstack security inspect project-a)"
grep -F "Security score" <<<"$security" >/dev/null
grep -F "A" <<<"$security" >/dev/null
grep -F "Docker socket" <<<"$security" >/dev/null
grep -F "absent" <<<"$security" >/dev/null
grep -F "OPENAI_API_KEY" <<<"$security" >/dev/null
if grep -F "$secret_value" <<<"$security" >/dev/null; then
  echo "Secret value leaked through security inspect" >&2
  exit 1
fi

doctor="$(run_hstack doctor project-a --security)"
grep -F "PASS" <<<"$doctor" >/dev/null
grep -F "score A" <<<"$doctor" >/dev/null
if grep -F "$secret_value" <<<"$doctor" >/dev/null; then
  echo "Secret value leaked through doctor" >&2
  exit 1
fi

# Encrypted secret material exists but plaintext must not be recoverable by a
# simple scan of HermesStack's persistent data.
test -d "$HSTACK_HOME/secrets"
if grep -R -F "$secret_value" "$HSTACK_HOME" >/dev/null 2>&1; then
  echo "Secret value leaked into HermesStack persistent files" >&2
  exit 1
fi

run_hstack down project-a
echo "M4 Network & Security end-to-end validation passed."
