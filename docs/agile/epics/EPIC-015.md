# EPIC-015 — Shared Memory & Context

**Business Goal:** provide durable cross-session context without weakening project isolation.

**Problem:** Herdr persists sessions, but durable knowledge, experience and skills require a context provider with explicit project boundaries.

**Scope:** provider abstraction, OpenViking deployment, project identities, private/shared scopes, context budgets, secret filtering, observability, first-party agent integrations and memory export/import.

**Out of Scope:** custom vector database, custom embedding pipeline, custom memory extraction, M7 full backup lifecycle.

**Expected Value:** ProjectA cannot retrieve ProjectB private memory; selected projects can access explicitly shared namespaces.

**Success Metrics:** provider-native project identity, restricted ACL sharing, explainable retrieval, no root/admin key in workspace state, green M6 CI.

**PBIs:** PBI-1501 through PBI-1509.

**Security Impact:** Critical. Project isolation and secret filtering are release blockers.

**Target Release:** M6.

**Status:** In Progress.
