# CI and push policy

HermesStack keeps GitHub Actions usage deliberately low during active development.

## Rules

- Do not push every small edit or fix.
- Work locally or through unreferenced Git objects and group related changes into a meaningful vertical increment.
- Prefer one consolidated remote commit for a completed story batch or milestone checkpoint.
- Normal pushes to `main` do **not** trigger GitHub Actions.
- Open a pull request only when a consolidated candidate is worth validating; the PR runs one Linux build/test/end-to-end gate.
- `workflow_dispatch` remains available for explicit manual validation; the optional Windows compile/unit-test job is off by default.
- Merge the exact validated candidate to `main`; the merge push itself does not trigger another run.
- Re-run CI only when a failed result requires a materially changed candidate.

This policy may be overridden explicitly for release validation or a security-critical change.
