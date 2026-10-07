# service-health-dashboard design

## How it works
The backend pings `api/platform/microservice/ping` on every service configured for the active
environment, all at once, and returns one row per service. The page shows a card per service,
refreshes every minute while it is open, and has a manual Refresh button.

## Decisions
- **Up means HTTP 200 and a `pong` body.** A gateway can answer 200 with a page of its own (an auth
  wall, a maintenance notice) while the service behind it is unreachable, so the status code alone
  would read as healthy. A 200 whose trimmed body is not `pong` (case-insensitively) counts as down.
- **A down service is data, not a failure.** The endpoint answers 200 with a row per service either
  way, and a down one carries the reason. The request fails only if the configuration itself can't be
  read, so the page reserves its error message for the request as a whole failing.
- **The service list comes from the active environment through service discovery.** Nothing is
  cached, so an environment switch shows on the next refresh. A service with no location is skipped
  with a warning rather than reported as down.
- **Refresh is a client-side timer.** There is no server push. A round still in flight blocks the
  next tick from starting another, since one round already fans out to every service on the server.

## Constraints and limitations
- Each ping times out after 10 seconds, and pings run concurrently, so a round takes as long as the
  slowest service, up to that timeout. A ping that slow is an outage worth showing anyway.
- An outage can take up to a minute to appear; Refresh gives an immediate read.
