# M5 Increment Report — Token Efficiency

## Delivered

- `ITokenOptimizer`, registry and project-scoped configuration.
- RTK v0.51.0 pinned with official release checksums.
- Caveman v2.7.0 pinned to a signed release commit.
- off/safe/balanced/aggressive/custom profiles.
- Compatibility policy with explicit consent for PotentiallyLossy stacking.
- `hstack token providers|status|enable|disable|configure|doctor|gain|stats`.
- Estimated/Measured/Unavailable evidence model.
- Privacy-safe durable aggregate metrics.
- `doctor --tokens` and M5 security-inspection fields.

## Privacy

HermesStack does not durably store RTK command history or raw recalled output. RTK's tracking DB is placed on workspace tmpfs, recall and telemetry are disabled, and only aggregate gain records are persisted by HermesStack.

## Validation

M5 CI runs Windows restore/build/unit tests and Linux restore/build/unit/integration, M3 session regression, M4 network/security regression and M5 Docker end-to-end validation. Post-merge distribution remains gated on both OS jobs.

## Known upstream limitation

RTK does not currently provide a stable JSON gain API or reliable per-agent attribution for HermesStack's use case. M5 therefore parses the human aggregate conservatively and reports Unavailable instead of fabricating per-agent metrics.

## Next

M6 / EPIC-015 — Shared Memory & Context, OpenViking first.
