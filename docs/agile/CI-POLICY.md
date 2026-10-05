# CI and push policy

HermesStack validates every pull request and every push to `main`.

## Required gates

- Linux restore/build/unit/integration tests.
- M3 session regression smoke.
- M4 network/security regression smoke.
- Current M5 token-efficiency Docker smoke.
- Windows restore/build/unit tests.
- Distribution artifacts are published only on a successful `main` push after both OS validation jobs succeed.

## Development rules

- Group related work into coherent vertical increments.
- Do not merge a red or incomplete pull request.
- Merge the exact tested head SHA.
- Treat post-merge `main` validation as part of the Definition of Done.
- Keep `workflow_dispatch` available for explicit manual validation.

M5 uses the `linux-m5` gate plus Windows validation. Published artifacts target `win-x64`, `linux-x64` and `linux-arm64`, each with a SHA-256 manifest.
