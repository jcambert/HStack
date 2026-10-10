#!/usr/bin/env bash
set -euo pipefail
echo '[M10] Secure local Blazor WASM + API smoke'
root="$(mktemp -d)"
port=59673
url="https://127.0.0.1:$port"
mkdir -p "$root/home" "$root/project"
server_pid=""
cleanup() {
  if [[ -n "$server_pid" ]]; then kill "$server_pid" >/dev/null 2>&1 || true; wait "$server_pid" 2>/dev/null || true; fi
  rm -rf "$root"
}
trap cleanup EXIT

# No Docker workspace is required to verify API/session/CSRF. The existing
# M3 and M8 Linux gates validate the real Compose/Aspire resource lifecycles.
HSTACK_HOME="$root/home" dotnet run \
  --project src/HermesStack.Web.Host/HermesStack.Web.Host.csproj \
  -c Release --no-build -- --port "$port" --no-browser >"$root/server.log" 2>&1 &
server_pid=$!

token=""
for _ in $(seq 1 150); do
  token="$(grep -Eo '#[A-F0-9]{64}' "$root/server.log" | head -1 | tr -d '#' || true)"
  [[ -n "$token" ]] && break
  if ! kill -0 "$server_pid" 2>/dev/null; then
    sed -E 's/#[A-F0-9]{64}/#REDACTED/g' "$root/server.log" >&2
    exit 1
  fi
  sleep 0.2
done
[[ -n "$token" ]] || { echo 'Missing local UI launch URL' >&2; exit 1; }

status() {
  curl -k -s -o /dev/null -w '%{http_code}' "$@"
}
[[ "$(status "$url/api/projects")" == 401 ]]
[[ "$(status -H 'Host: attacker.example' "$url/api/projects")" == 403 ]]
[[ "$(status -H 'Origin: https://attacker.example' "$url/")" == 403 ]]
[[ "$(status -X POST -H 'Content-Type: application/json' -d '{"token":"invalid"}' "$url/unlock")" == 401 ]]

# The launch token lives only in a URL fragment and a one-time POST body,
# never as a query string, cookie or browser-storage credential.
[[ "$(status -c "$root/cookies" -H "Origin: $url" -H 'Content-Type: application/json' \
       --data "{\"token\":\"$token\"}" "$url/unlock")" == 204 ]]
[[ "$(status -H "Origin: $url" -H 'Content-Type: application/json' \
       --data "{\"token\":\"$token\"}" "$url/unlock")" == 401 ]]
[[ "$(status -b "$root/cookies" "$url/api/projects")" == 200 ]]
[[ "$(status -b "$root/cookies" -H 'Origin: https://attacker.example' "$url/api/projects")" == 403 ]]
[[ "$(status -b "$root/cookies" -X POST -H "Origin: $url" \
       -H 'Content-Type: application/json' -d '{}' "$url/api/projects")" == 403 ]]
[[ "$(status -b "$root/cookies" -X POST -H 'Content-Type: application/json' \
       -d '{}' "$url/api/projects")" == 403 ]]
[[ "$(status -b "$root/cookies" -X POST -H 'Origin: https://attacker.example' \
       -H 'X-HStack-CSRF: bogus' -H 'Content-Type: application/json' \
       -d '{}' "$url/api/projects")" == 403 ]]

csrf="$(curl -k -fsS -b "$root/cookies" "$url/api/session" | jq -r '.csrf')"
[[ "$csrf" =~ ^[A-F0-9]{64}$ ]]

echo '[M10] Session and hostile-origin/CSRF checks passed'
# Access a real, bundled WASM asset and the Markdown user manual.
echo '[M10] Verify authenticated index and WASM framework'
curl -k -fsS -b "$root/cookies" "$url/" | grep -q 'HermesStack'
[[ "$(status -b "$root/cookies" "$url/_framework/blazor.webassembly.js")" == 200 ]]
echo '[M10] Verify bundled Markdown guide'
curl -k -fsS -b "$root/cookies" "$url/api/help/start" | grep -q 'Guide utilisateur'

echo '[M10] Verify project and CLI state parity'
# A project registered from the UI must appear in the SAME CLI YAML database.
data="$(jq -nc --arg path "$root/project" '{id:"m10-demo",name:"M10 Demo",hostPath:$path}')"
[[ "$(status -b "$root/cookies" -H "Origin: $url" -H "X-HStack-CSRF: $csrf" \
       -H 'Content-Type: application/json' -d "$data" "$url/api/projects")" == 201 ]]
curl -k -fsS -b "$root/cookies" "$url/api/projects" | jq -e '.[] | select(.id=="m10-demo")' >/dev/null
grep -q 'm10-demo' "$root/home/config/projects.yaml"
HSTACK_HOME="$root/home" dotnet run --project src/HermesStack.Cli/HermesStack.Cli.csproj \
  -c Release --no-build -- project list | grep -q 'm10-demo'

# No arbitrary process execution route. Explicit destructive confirmations
# are required even with a valid session and CSRF token.
[[ "$(status -b "$root/cookies" "$url/api/run-command")" == 404 ]]
[[ "$(status -b "$root/cookies" -X DELETE -H "Origin: $url" \
       -H "X-HStack-CSRF: $csrf" "$url/api/projects/m10-demo")" == 400 ]]
[[ "$(status -b "$root/cookies" -X DELETE -H "Origin: $url" \
       -H "X-HStack-CSRF: $csrf" -H 'X-HStack-Confirm: delete:m10-demo' \
       "$url/api/projects/m10-demo")" == 204 ]]
[[ -d "$root/project" ]]
[[ "$(status -b "$root/cookies" "$url/api/projects")" == 200 ]]
echo '[M10] Secure same-origin API + Blazor assets + CLI project parity passed'
