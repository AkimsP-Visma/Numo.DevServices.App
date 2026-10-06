# api-catalog-browser design

## How it works
The page lists the services configured for the active environment and renders the chosen one's
OpenAPI document in Swagger UI. The backend fetches that document from the service itself, at the
one fixed path every Numo service serves (`swagger/v1/swagger.json`), and rewrites its `servers`
before returning it. Swagger UI is given this backend's URL for the document and does the fetch and
parse itself; the page never handles the JSON.

## Decisions
- **One narrow endpoint per need, never a pass-through.** The backend only ever requests the fixed
  document path on a service named by its configuration key. It is this capability's instance of
  the `platform-conventions` pass-through rule: this backend runs next to the real services and can
  reach them, so a route forwarding any caller-supplied path would be a hole into the internal
  network. The cost is that a different document from a service needs a new handler, not a
  parameter.
- **The served document gets the service's configured location prepended as its first server.**
  Numo services declare relative servers (`/employee-api`, `/`), which would resolve against this
  app's origin, where no matching route exists. With the absolute location first, "Try it out"
  targets the real service.
- **The service list follows the active environment.** It comes from the environment-aware service
  discovery, read on every call, so a switch changes the picker and the document fetch on the next
  request. A service with no location in the current environment is skipped with a warning rather
  than offered as a broken entry.
- **Swagger UI ships as a lazily loaded bundle.** Its scripts and styles are `inject: false`
  bundles, pulled in on demand, like any other heavy third-party UI in this app.

## Constraints and limitations
- **"Try it out" reaches the service, but the browser blocks the response.** Numo services send no
  `Access-Control-Allow-Origin` and answer `OPTIONS` with 405. Fixing that in the deployed app would
  need the pass-through route this app refuses to have, so the page says so in a notice. Locally, a
  CORS-disabling browser extension or proxying the service prefix in `proxy.conf.js` works around
  it; neither adds anything to the deployed app.
- A service that does not serve its document at the fixed path fails to load, and there is no
  per-service override. Every current Numo service follows the convention.
