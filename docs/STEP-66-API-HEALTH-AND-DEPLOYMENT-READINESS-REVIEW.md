# Step 66 — API health and deployment-readiness review

## Scope
Read-only review of the API host, health endpoints, exception pipeline, and deployment posture. The only code change removes request-rate limiting from operational health probes and preserves cancellation semantics for readiness checks.

## Findings

- `/health` is a liveness probe backed by the default ASP.NET Core health-check service. No database health check is registered, so liveness remains independent of PostgreSQL availability.
- `/health/ready` is a separate database-connectivity probe. It returns HTTP 503 when the connection string is absent or PostgreSQL cannot be reached, and returns only generic status values; connection strings and exception messages are not sent to clients.
- The readiness query is `SELECT 1`. It confirms connection/query reachability only; it does not verify migration version, schema integrity, grants, RLS behavior, tenant isolation, backups, or clinical readiness.
- Health probes are no longer attached to the general API rate-limit policy. Monitoring and orchestrators can continue probing during API traffic spikes without consuming or being blocked by client request quotas.
- A request/host cancellation now propagates from the readiness handler instead of being converted into a misleading database-unavailable response.
- The exception middleware returns a generic HTTP 500 response. Its client response does not include exception messages or stack traces.
- Clinical patient, procedure, report, media, and platform-administration endpoints remain deliberately unmapped by the API host. No clinical route was enabled by this review.

## Remaining deployment requirements

- Configure trusted forwarded headers only for known reverse proxies before using forwarded client IPs for production rate limiting.
- Add authenticated/controlled production ingress, TLS termination, secret management, pinned container image digests, network policy, backups, and tested restore procedures.
- Add a migration/schema readiness gate and deployment-specific health monitoring; current readiness is connectivity-only.
- Complete independent security, privacy, operational, and clinical validation before any real patient data or clinical use.

## Verification boundaries

- Source-level review and offline structural assertions are recorded in this step.
- No PostgreSQL instance was started, no migration or grant was applied, no patient data was accessed, and no clinical endpoint was activated.
- CI status must be checked on the resulting commit. An absent workflow result is not a passing build.
- This remains a development scaffold, not a production deployment or clinically validated system.
