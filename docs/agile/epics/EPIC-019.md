# EPIC-019 — CLI UX & Interactive Dashboard

**Business Goal:** make daily HermesStack operations discoverable and automation-friendly.

**Scope:** interactive Spectre.Console dashboard, complete project lifecycle commands, loopback port management, explicit agent status, status JSON/quiet output, config validation, operational help and safe Compose escape hatch.

**Success Metrics:** the no-argument CLI exposes an interactive action menu on a real terminal, project list/show/add/edit/remove are available, declared ports bind to 127.0.0.1 by default, `status --json` is machine-readable, config validation uses stable errors and Spectre literal brackets render correctly.

**PBIs:** PBI-1901.

**Target Release:** M7.

**Status:** Review.
