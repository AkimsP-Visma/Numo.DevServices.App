## Context

This is a retroactive design record, not a forward-looking one: it documents the decisions already
made in the shipped code under `Features/ServiceHealth/` and `Features/Environments/`. There is no
implementation work attached to this change - see `tasks.md`.

## Goals / Non-Goals

**Goals:**
- Capture why "up" requires HTTP 200 *and* a literal `pong` body, not status code alone.
- Capture why one service being down is modeled as data in the result set, not a request failure.
- Capture where the list of services-to-ping comes from and why it is environment-scoped.
- Capture the refresh mechanism the page uses while open.

**Non-Goals:**
- Changing any of the above. No behavior in this area is altered by this change.
- Revisiting the open question in `docs/Purpose.md` about moving the service registry into the
  database - still unresolved, not addressed here.

## Decisions

- **Up means HTTP 200 and a `pong` body, not status code alone.**
  `ServicePing.PingAsync` (`Features/ServiceHealth/ServicePing.cs`) treats any non-200 status as down,
  and separately treats a 200 whose trimmed body does not case-insensitively equal `pong` as down too.
  This exists because a gateway can sit in front of a service and answer 200 with its own page (an
  auth wall, a maintenance notice) while the service behind it is actually unreachable - the status
  code alone would then read as healthy. Requiring the exact expected body closes that gap. Rejected
  alternative: trusting the status code alone, which is exactly the failure mode this guards against
  (`docs/Purpose.md`, "Every Numo service answers...").

- **One service being down is data, not a request failure.**
  `GetServiceHealthHandler.HandleAsync` calls every configured service's ping concurrently via
  `Task.WhenAll` and always returns `NumoResult.Ok` with a full `GetServiceHealthResult` - a down
  service produces a `ServiceHealthStatus` row with `IsUp: false` and a `DownReason`, never an
  exception or a `NumoError`. The request only fails if the service configuration itself cannot be
  read. This matches the standing constraint in `docs/Purpose.md`: "the endpoint answers 200 with a
  row per service either way." It also means the frontend never has to special-case a single service
  outage as an overall page error - `errorMessage` is reserved for the request as a whole failing
  (network error, problem-details response).

- **The services-to-ping list comes from the active environment, via `IServiceDiscoveryService`.**
  `GetServiceHealthHandler` depends only on `IServiceDiscoveryService` (from `Numo.Common.Lib`), not on
  configuration directly. `EnvironmentAwareServiceDiscovery` (`Features/Environments/`) is the
  registered implementation and reads `CurrentEnvironmentStore.CurrentDefinition.Services` - the
  service-location map for whichever environment key `CurrentEnvironmentStore` currently holds
  (Testing, Staging, Production or Local; see `EnvironmentsOptions`). Nothing in `ServiceHealth`
  caches anything, so a runtime environment switch is visible on the very next health-check request.
  A service entry with no configured `Location` is skipped before pinging (logged as a warning), the
  same "discovery yields an entry even when Location is missing" behavior the handler's own comment
  documents. This indirection is why the ServiceHealth code needed no change when the registry moved
  from a flat `Services` section to the per-environment `Environments` section - it always depended on
  the interface, never the concrete discovery implementation.

- **Refresh is a client-side interval timer, not server push.**
  `ServiceHealthPage` starts `timer(0, REFRESH_INTERVAL_MS)` (60 seconds) in its constructor, wired
  through `takeUntilDestroyed()` so it stops when the page is navigated away from - there is no
  websocket or long-poll. `load()` is a no-op re-entry guard while a previous round is still in
  flight (`if (this.isLoading()) return;`), because a ping round can outlive the interval when a
  service hangs up to the 10-second-per-service `PingRequestTimeout`, and pinging every configured
  service is one request already fanned out server-side. A manual "Refresh" button calls the same
  `load()` for an on-demand check between ticks.

## Risks / Trade-offs

- [A hung service adds up to its 10-second timeout to every refresh round, and all pings run
  concurrently so the round's total latency is bounded by the slowest service, not the sum] →
  Mitigated by the per-client `PingRequestTimeout` (10s) in `ServiceHealthRegistration`, chosen
  because "a ping that takes this long is a dashboard-worthy outage anyway."
- [The client-side 60-second timer means the dashboard can show a stale snapshot for up to a minute
  after a real outage starts] → Accepted for a developer-only tool; the manual Refresh button exists
  for when a fresher read is wanted immediately.

## Open Questions

None specific to this capability. The repo-wide open question about eventually moving the service
registry into the database (`docs/Purpose.md`, "Open questions") applies here too, since ServiceHealth
consumes that registry through `IServiceDiscoveryService`, but resolving it is out of scope for this
retroactive baseline.
