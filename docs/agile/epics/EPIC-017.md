# EPIC-017 — Backup, Restore & Portability

**Business Goal:** make HermesStack operational state recoverable and portable without copying project source trees by default.

**Problem:** configuration, project metadata, certificates, agent/session state and context state must survive host failures and controlled migrations.

**Scope:** local backup/restore, selective project backup, config-only backup, portable environment export/import and path remapping.

**Out of Scope:** backing up project source directories; unencrypted portable secret export.

**Expected Value:** an operator can recover HermesStack state or move its control-plane configuration to another host safely.

**Success Metrics:** archive format is versioned, traversal-safe, excludes workspace source, includes durable OpenViking state in full backup, and import fails explicitly when project paths need remapping.

**PBIs:** PBI-1701, PBI-1702.

**Security Impact:** High. Archives may contain sensitive operational metadata and certificates.

**Target Release:** M7.

**Status:** Review.
