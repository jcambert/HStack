# CI and push policy

HermesStack validates every pull request and every push to `main`.

## Required gates

- Linux restore/build/unit/integration tests.
- M3 Sessions, M4 Network/Security, M5 Token Efficiency, M6 Shared Context, M7 Operations regression gates.
- **M9 Native Provider security/unit tests** run in the existing Linux and Windows unit-test steps (no duplicate Docker image build). M3 Compose + M8 Aspire E2E smoke remain the real OCI parity gates and M8 adds M9 host-namespace/mount checks.
- **M8 Aspire end-to-end runtime gate** (`tests/e2e/m8-smoke.sh`) with the pinned official CLI 13.6.0.
- Windows restore/build/unit tests.
- Delivery candidate packaging for win-x64, linux-x64, linux-arm64; verify each SHA-256 manifest before upload.
- On successful `main` push, publish the same three verified distribution archives.

M8 reuses the fully pinned agent image built and verified earlier in the *same Linux job*, only after checking image kind, exact agent versions and toolchain SHA-256. Base-image substitution is forbidden in the M8 gate.

## Development rules

- Group work in coherent vertical increments.
- Do not merge a red or incomplete pull request.
- Merge only the exact tested head SHA; post-merge validation on `main` is part of Definition of Done.
- Keep `workflow_dispatch` available for manual validation; Windows checks remain mandatory for normal PR/push workflows.
- Never disable a required security or runtime test solely to obtain a green CI.

### M10 UI security and delivery checks

The required Linux job executes `tests/e2e/m10-web-smoke.sh` and `tests/e2e/m10-browser-smoke.sh` after unit/integration tests and before rebuilding the Docker agent image, so Web failures are detected early. They verify the one-time local HTTPS bootstrap, origin/host/CSRF and replay rejection, CLI/REST project-store parity and actual headless Chrome WASM rendering (not merely HTML download). Existing M3–M9 runtime Docker/Aspire/agent checks remain required. The three-RID publish job verifies executable Web host, packaged manual and WASM assets and SHA-256 manifests; it does **not** certify native Windows-browser/ARM64 execution.
