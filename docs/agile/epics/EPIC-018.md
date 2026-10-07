# EPIC-018 — Observability & Metrics

**Business Goal:** make local operations diagnosable without leaking credentials.

**Scope:** HermesStack application log, Docker workspace logs, existing token/context metrics, stable redaction and operator-facing diagnostics.

**Expected Value:** operators can inspect control-plane failures and workspace output while sensitive tokens remain redacted.

**Success Metrics:** `.hstack/logs/hstack.log` exists for command/failure events, logs use the existing secret redactor, and `hstack logs <project>` streams backend logs.

**PBIs:** PBI-1801.

**Security Impact:** High because logs are a common secret-exfiltration surface.

**Target Release:** M7.

**Status:** Done.
