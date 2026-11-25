# SCOPX.I.E — Architecture Decisions v1.0


## AD-001: Modular monolith first
**Status:** Proposed. **Decision:** Begin with a modular ASP.NET Core backend rather than microservices. **Why:** reduces operational complexity for an initial team and enables clear module boundaries. Revisit when scale/team needs justify service extraction.

## AD-002: Server-side tenant enforcement
**Status:** Proposed. **Decision:** Hospital context and membership are validated on the server for every request. Client-provided tenant IDs are hints only and never trusted. **Why:** prevents cross-tenant access via manipulated requests.

## AD-003: Desktop capture separated behind adapters
**Status:** Proposed. **Decision:** endoscopy hardware/capture integration is isolated behind interfaces/adapters. **Why:** vendor SDKs, capture cards, and device models differ; integration must be tested on the actual setup.

## AD-004: AI assistance with human approval
**Status:** Proposed. **Decision:** AI outputs are draft/assistive and require clinician review; no autonomous signing or silent changes. **Why:** patient safety and traceability.

## AD-005: Synthetic data during development
**Status:** Proposed. **Decision:** no real patient data in local development or demos. **Why:** reduce privacy and security risk before controls and compliance review.

## AD-006: Desktop client candidate
**Status:** Proposed. **Decision:** WPF is the initial Windows desktop candidate; evaluate deployment and UI needs before locking. **Why:** native Windows device/capture integrations may be necessary.

## AD-007: Database candidate
**Status:** Proposed. **Decision:** PostgreSQL is the primary relational database candidate. Evaluate row-level security alongside application-level tenant filters. **Why:** strong relational support and robust access controls.
