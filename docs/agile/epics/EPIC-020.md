# EPIC-020 — CI/CD & Distribution

**Business Goal:** continuously validate and distribute reproducible HermesStack binaries on supported platforms.

**Scope:** Linux/Windows build/test gates, M3-M7 regression gates, self-contained single-file publication and SHA-256 delivery manifests.

**Expected Value:** every merge to main is validated and produces portable delivery artifacts for win-x64, linux-x64 and linux-arm64.

**Success Metrics:** green Linux and Windows CI; self-contained artifacts for all three RIDs; SHA-256 manifest generated for every package.

**PBIs:** PBI-2001.

**Target Release:** M7.

**Status:** Review.
