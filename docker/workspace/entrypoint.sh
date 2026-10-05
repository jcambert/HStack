#!/bin/sh
set -eu

bundle_dir="$HOME/.hstack/certs"
bundle="$bundle_dir/ca-bundle.crt"
mkdir -p "$bundle_dir"

if [ -n "${HSTACK_CORPORATE_CA_FILE:-}" ] && [ -f "$HSTACK_CORPORATE_CA_FILE" ]; then
  cat /etc/ssl/certs/ca-certificates.crt "$HSTACK_CORPORATE_CA_FILE" > "$bundle.tmp"
  mv "$bundle.tmp" "$bundle"
else
  cp /etc/ssl/certs/ca-certificates.crt "$bundle"
fi

exec "$@"
