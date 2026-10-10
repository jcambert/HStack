#!/usr/bin/env bash
set -euo pipefail
echo '[M10] Headless Chrome real Blazor WASM render and secure bootstrap'
root="$(mktemp -d)"
port=59674
url="https://127.0.0.1:$port"
server_pid=""
cleanup() {
  if [[ -n "$server_pid" ]]; then kill "$server_pid" >/dev/null 2>&1 || true; wait "$server_pid" 2>/dev/null || true; fi
  rm -rf "$root"
}
trap cleanup EXIT
mkdir -p "$root/home"
chrome="$(command -v google-chrome || command -v chromium || command -v chromium-browser || true)"
if [[ -z "$chrome" ]]; then
  echo '[M10] Chrome/Chromium is mandatory for browser E2E and missing' >&2
  exit 1
fi
HSTACK_HOME="$root/home" dotnet run \
 --project src/HermesStack.Web.Host/HermesStack.Web.Host.csproj \
 -c Release --no-build -- --port "$port" --no-browser > "$root/server.log" 2>&1 &
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
[[ -n "$token" ]] || { echo '[M10] no browser launch token' >&2; exit 1; }

# The real Chromium engine follows the one-use URL-fragment bootstrap,
# accepts the temporary local self-signed certificate for CI only, loads
# Blazor WebAssembly and renders the actual MudBlazor project dashboard.
timeout 90 "$chrome" \
  --headless=new --no-sandbox --disable-dev-shm-usage \
  --ignore-certificate-errors --no-first-run --no-default-browser-check \
  --user-data-dir="$root/chrome-profile" \
  --virtual-time-budget=30000 --dump-dom "$url/#$token" \
  >"$root/browser.html" 2>"$root/browser.log" || {
    echo '[M10] Chrome failed to start or render' >&2
    tail -n 50 "$root/browser.log" >&2
    exit 1
  }

if ! grep -q 'Ton atelier de développement' "$root/browser.html"; then
  echo '[M10] Blazor WASM dashboard was not rendered' >&2
  grep -E 'Exception|ERR_|error|Failed' "$root/browser.log" | tail -n 30 >&2 || true
  head -c 2500 "$root/browser.html" >&2
  exit 1
fi
grep -q 'Centre de contrôle' "$root/browser.html"
grep -q 'Guide utilisateur' "$root/browser.html"
echo '[M10] Real headless Chrome rendered the authenticated MudBlazor dashboard'
