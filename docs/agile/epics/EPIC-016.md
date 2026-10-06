# EPIC-016 — Update & Toolchain Management

**Business Goal:** keep HermesStack and its managed workspace toolchain current without sacrificing reproducibility or rollback safety.

**Problem:** agents and integration tools evolve independently; ad-hoc in-workspace upgrades would create drift and make rollback unreliable.

**Scope:** managed update metadata, version checks, checksum validation, transactional update planning, image rebuild/recreate, health validation and rollback.

**Out of Scope:** silent background upgrades, unpinned `latest` dependencies, manual package upgrades inside running workspaces.

**Expected Value:** operators can see approved updates before changing anything and later apply them through a controlled transaction.

**Success Metrics:** deterministic update plan, HTTPS-only metadata, pinned artifacts/checksums, green Linux/Windows CI and no mutation during `update check`.

**PBIs:** PBI-1601 and follow-up transactional update PBIs.

**Security Impact:** High. Update metadata and artifacts are part of the software supply chain.

**Target Release:** M7.

**Status:** In Progress.
