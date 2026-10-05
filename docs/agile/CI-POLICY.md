# CI and push policy

HermesStack keeps GitHub Actions usage deliberately low during active development.

## Rules

- Do not push every small edit or fix.
- Work locally and group related changes into a meaningful vertical increment.
- Push at stable checkpoints: completed user story, substantial vertical slice, or milestone checkpoint.
- Prefer one consolidated remote commit for a batch when intermediate commits have no review value.
- GitHub Actions CI is not triggered by ordinary pushes to `main`.
- CI runs on pull requests to `main` or by explicit `workflow_dispatch`.
- Re-run CI only when the result can materially validate a new batch of changes.

Local commits are allowed for safety and rollback because they do not consume GitHub Actions minutes until pushed.

This policy may be overridden explicitly for release validation or a security-critical change.
