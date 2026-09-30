## Why

The service status dashboard (`Features/ServiceHealth/` plus the `/service-health` page) is already
built and shipped - see `docs/Purpose.md` feature 4. OpenSpec was only just initialized in this repo,
so no spec exists yet for behavior that has been live for a while. This change adds no behavior; it
retroactively documents the already-shipped feature so OpenSpec has a baseline capability spec to
diff future changes against, rather than proposing new work.

## What Changes

- Document the existing ping dashboard as a new OpenSpec capability, `service-health-dashboard`,
  covering: pinging every service configured for the active environment, the up/down determination
  rule (HTTP 200 and a literal `pong` body, not status code alone), rendering one row per service
  regardless of individual failures, and the page's periodic auto-refresh while open.
- No code changes. No new endpoints, fields, or UI behavior are introduced by this change.

## Capabilities

### New Capabilities
- `service-health-dashboard`: pings every service configured for the active environment's
  `api/platform/microservice/ping` endpoint, determines up/down from status code plus body content,
  and renders a live-refreshing status grid with one row per service regardless of individual
  outcomes.

### Modified Capabilities
(none - this is the first spec for this behavior)

## Impact

- Affected code (read-only, not modified by this change): `src/Numo.DevServices.Api/Features/ServiceHealth/`
  (`GetServiceHealth.cs`, `ServicePing.cs`, `ServiceHealthController.cs`, `ServiceHealthRegistration.cs`),
  `src/Numo.DevServices.Api/Features/Environments/` (`EnvironmentAwareServiceDiscovery.cs`,
  `CurrentEnvironmentStore.cs`, `EnvironmentsOptions.cs`), and
  `src/Numo.DevServices.Api/ClientApp/src/app/features/service-health/` (API service, model, and
  page component/template).
- No API, schema, or dependency changes. Purely a documentation baseline under `openspec/`.
