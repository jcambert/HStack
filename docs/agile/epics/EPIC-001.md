# EPIC-001 — Foundation & Control Plane

**Business Goal:** provide one local CLI and configuration root for secure agent workspaces.  
**Problem:** upstream tools have incompatible lifecycle/configuration surfaces and should not be run directly on the host for managed sessions.  
**Scope:** .NET control plane, configuration, process abstraction, initialization, integration registry.  
**Out of Scope:** agent installation in M1.  
**Expected Value:** reproducible entry point and stable policy model.  
**Success Metrics:** `hstack init`, `project add/list`, `up/down/shell/status` operate end-to-end.  
**Dependencies:** Docker Desktop / Compose for runtime.  
**Risks:** host path mistakes, upstream drift.  
**Security Impact:** critical.  
**Target Release:** M1.  
**Status:** In Progress.
