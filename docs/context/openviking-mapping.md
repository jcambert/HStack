# OpenViking mapping — M6 Shared Context

HermesStack integrates OpenViking as the durable context provider. OpenViking owns retrieval, memory extraction, resources, skills, sessions and the `viking://` model; HermesStack owns deployment, access policy, project association, secret filtering and diagnostics.

## Project identity

HermesStack uses one OpenViking account, `hstack`, and provisions one OpenViking user per project. Each project receives a distinct user API key. Private context is addressed through `viking://~/...`, where `~` resolves from the authenticated project user.

The OpenViking root and account-admin credentials remain host-side and are never injected into agent workspaces.

## Logical scopes

| Scope | Mapping | Policy |
|---|---|---|
| session | OpenViking session lifecycle | project user only |
| agent | `viking://~/peers/<agent>/memories/` | project user + peer |
| project | `viking://~/` | dedicated project user |
| shared | `viking://resources/hstack-shared/<namespace>/` | explicit restricted ACL |
| global | denied in M6 | use explicit shared namespace |

## Shared-resource ACL enforcement

HermesStack explicitly enables OpenViking account-level `settings.acl.enabled` for the `hstack` account through the provider Admin API before shared namespaces are used. OpenViking 0.4.23 leaves this switch disabled by default; creating a `restricted` ACL without enabling the account switch would not enforce access checks. Failure to enable ACL enforcement is therefore a fail-closed M6 startup/provisioning error.

## Automatic-capture secret filtering

HermesStack-managed writes pass through `IContextSecretFilter`. Project OpenViking client configuration also applies upstream `plugin.captureFilters` before first-party agent capture is stored. The configured rules cover common provider-token forms, credential assignments and private-key material. This keeps the provider integration first-party while adding HermesStack's required policy layer.

## Credential handling

The server configuration contains an environment placeholder for `OPENVIKING_ROOT_API_KEY`. Docker receives the value through a runtime secret file with owner-only permissions. Project user credentials live only in the protected secret store and project-specific OpenViking client state.

## Budget and filtering

The default retrieval budget is 12,000 estimated tokens and 20 items. Durable writes pass through `IContextSecretFilter`: private keys are rejected and known token patterns are redacted.

## Agent integration

Claude Code, Codex and OpenCode use the official OpenViking unified installer. Hermes uses the upstream OpenViking memory setup. HermesStack invokes installers with structured commands rather than a downloaded shell pipeline.
