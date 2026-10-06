# Sourced by network-facing RUN steps of Dockerfile.full.
# Trusts the optional corporate CA bundle (BuildKit secret) for the current step
# only: nothing is written to the image beyond the public bundle in /tmp, which
# the final cleanup step removes.
hstack_ca_secret=/run/secrets/hstack_corporate_ca
if [ -s "$hstack_ca_secret" ]; then
  cat /etc/ssl/certs/ca-certificates.crt "$hstack_ca_secret" > /tmp/hstack-build-ca.crt
  export SSL_CERT_FILE=/tmp/hstack-build-ca.crt
  export CURL_CA_BUNDLE=/tmp/hstack-build-ca.crt
  export REQUESTS_CA_BUNDLE=/tmp/hstack-build-ca.crt
  export GIT_SSL_CAINFO=/tmp/hstack-build-ca.crt
  export NODE_EXTRA_CA_CERTS=/tmp/hstack-build-ca.crt
fi
