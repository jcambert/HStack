#!/usr/bin/env bash
set -euo pipefail
echo '[M10] Launch the real UI through an unpackaged solution-build CLI'
cli="$PWD/src/HermesStack.Cli/bin/Release/net10.0/hstack"
host="$PWD/src/HermesStack.Web.Host/bin/Release/net10.0/hstack-web"
test -x "$cli" || { echo 'CLI build output is missing'; exit 1; }
test -x "$host" || { echo 'Web.Host build output is missing'; exit 1; }
# This MUST remain absent: a source build is not the published layout.
test ! -e "$(dirname "$cli")/web/hstack-web" ||
  { echo '[M10] Test would accidentally use the packaged path'; exit 1; }

root="$(mktemp -d)"
port=59743
url="https://127.0.0.1:$port"
pid=""
cleanup() {
  if [[ -n "$pid" ]]; then
    pkill -TERM -P "$pid" >/dev/null 2>&1 || true
    kill "$pid" >/dev/null 2>&1 || true
    wait "$pid" 2>/dev/null || true
  fi
  rm -rf "$root"
}
trap cleanup EXIT
mkdir -p "$root/home"
(
  cd "$root"
  HSTACK_HOME="$root/home" "$cli" ui --port "$port" --no-browser
) >"$root/web.log" 2>&1 &
pid=$!
token=""
for _ in $(seq 1 150); do
  token="$(grep -Eo '#[A-F0-9]{64}' "$root/web.log" | head -1 | tr -d '#' || true)"
  [[ -n "$token" ]] && break
  if ! kill -0 "$pid" 2>/dev/null; then
    sed -E 's/#[A-F0-9]{64}/#REDACTED/g' "$root/web.log" >&2
    exit 1
  fi
  sleep 0.2
done
[[ -n "$token" ]] || { echo '[M10] No launch URL from Visual Studio layout' >&2; exit 1; }
status() { curl -ks -o /dev/null -w '%{http_code}' "$@"; }
[[ "$(status "$url/api/projects")" == 401 ]]
[[ "$(status -H 'Host: untrusted.test' "$url/")" == 403 ]]
[[ "$(status -c "$root/cookies" -H "Origin: $url" -H 'Content-Type: application/json' \
  --data "{\"token\":\"$token\"}" "$url/unlock")" == 204 ]]
[[ "$(status -b "$root/cookies" "$url/api/projects")" == 200 ]]
[[ "$(status -b "$root/cookies" "$url/_framework/blazor.webassembly.js")" == 200 ]]
echo '[M10] Source solution CLI auto-finds Web.Host safely; session and WASM OK'
