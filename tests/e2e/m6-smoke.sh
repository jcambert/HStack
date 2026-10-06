#!/usr/bin/env bash
set -euo pipefail

IMAGE='ghcr.io/volcengine/openviking:v0.4.23@sha256:fb4cd789fd3303509edd1c10437ff63d4fab56b622494ac621f1fda042540ed4'

echo '[M6] Verify pinned OpenViking release image'
docker pull "$IMAGE"
docker run --rm "$IMAGE" openviking-server --help >/tmp/hstack-openviking-help.txt
grep -qi 'openviking' /tmp/hstack-openviking-help.txt

echo '[M6] Verify provider security invariants'
grep -q '127.0.0.1:1933:1933' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'no-new-privileges:true' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'cap_drop' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'OPENVIKING_ROOT_API_KEY' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
! grep -q 'server\["root_api_key"\] = rootKey' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs

echo '[M6] Verify project isolation and explicit sharing policy'
grep -q 'dedicated project user/API key' src/HermesStack.Cli/MemoryCliService.cs
grep -q 'Global context is denied in M6' src/HermesStack.Application/Context/OpenVikingContextScopeMapper.cs
grep -q 'acl_mode = "restricted"' src/HermesStack.Docker/Context/OpenVikingContextProvider.cs
grep -q 'EnsureRestrictedSharedNamespaceAsync' src/HermesStack.Docker/Context/OpenVikingContextProvider.cs
grep -q 'captureFilters' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'redacted-private-key' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'EnsureAccountAclEnabledAsync' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q '/api/v1/admin/accounts/.*configuration' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'enabled = true' src/HermesStack.Docker/Context/OpenVikingServiceManager.cs
grep -q 'IContextScopePolicy' src/HermesStack.Application/Abstractions/IContextProvider.cs
grep -q 'ContextScopePolicy' src/HermesStack.Application/Context/ContextService.cs
grep -q 'ClearAsync' src/HermesStack.Docker/Context/OpenVikingContextProvider.cs
grep -q 'memory clear' src/HermesStack.Cli/Program.cs

echo '[M6] Verify first-party integrations avoid shell pipelines'
grep -q 'openviking-install.sh' src/HermesStack.Cli/MemoryCliService.cs
! grep -q 'curl -fsSL https://openviking.ai/install | bash' src/HermesStack.Cli/MemoryCliService.cs

echo '[M6] Shared Context smoke checks passed'
