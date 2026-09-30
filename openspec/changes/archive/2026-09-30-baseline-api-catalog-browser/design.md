## Context

This documents the design already embodied in `Features/Services/` and the `/swagger` frontend page,
per `docs/Purpose.md`'s design decisions and `GetServiceOpenApi.cs`/`GetServices.cs`. No new design
work is proposed; this records the rationale for choices already made in the shipped code.

## Goals / Non-Goals

**Goals:**
- Capture why the backend fetches and rewrites each service's OpenAPI document itself rather than
  letting the browser call the service directly or acting as a generic reverse proxy.
- Capture why "Try it out" in the deployed app does not fully work, and what closes that gap only in
  local development.
- Capture how the served document's `servers` array gets the service's real, configured location.

**Non-Goals:**
- Changing any of the above. This is a documentation-only baseline.
- Designing the "own request UI" idea from `docs/Purpose.md`'s "Ideas for later" - that is explicitly
  future, unbuilt work, and out of scope here.

## Decisions

- **A narrow, single-purpose endpoint per need, not a pass-through proxy.**
  `GetServiceOpenApiHandler` only ever requests the fixed path `swagger/v1/swagger.json` on a service
  named by a configuration key (`IServiceDiscoveryService.GetServiceLocation`); it never accepts or
  forwards an arbitrary path from the caller. Per `docs/Purpose.md`, a pass-through route that forwarded
  any path would become a hole into the internal network if this developer tool were ever exposed
  outside it, since this backend - unlike a developer's own machine - is deployed alongside the real
  services and can reach them. Rejected alternative: a generic `/api/proxy?target=` style route, which
  would remove the need for a new backend action per feature but reopens that hole.

- **The backend rewrites `servers` to prepend the service's real location.**
  Numo services declare relative servers (e.g. `/employee-api`, `/`) in their generated OpenAPI
  documents, which resolve against whatever origin loaded the document - this app's own origin, since
  the frontend fetches the document from `api/services/{name}/openapi`, not from the service directly.
  `PrependConfiguredServer` inserts the service's configured absolute location as the first `servers`
  entry (labelled "Configured location, called straight from the browser") so Swagger UI's "Try it
  out" targets the real service instead of looping back into this app, which has no matching route.

- **"Try it out" reaching the service is still blocked by CORS in the deployed app, by design.**
  Once a request targets the real service, the browser still enforces CORS: Numo services send no
  `Access-Control-Allow-Origin` header and answer `OPTIONS` preflights with 405. Making Execute fully
  work in the deployed app would require the pass-through proxy route rejected above, so it is left
  not working there; the on-page notice in `swagger-page.html` tells the developer this. Two
  documented ways around it exist, both developer-workstation-only and adding nothing to the deployed
  app: a CORS-disabling browser extension (what the team uses today, accepted for a developer-only
  tool), or proxying the service's prefix in `proxy.conf.js` during `ng serve` so the request becomes
  same-origin.

- **The service list is environment-aware, not a flat, single list.**
  `GetServicesHandler` reads `IServiceDiscoveryService.GetAllServices()`, whose current implementation
  (`EnvironmentAwareServiceDiscovery`) resolves against `CurrentEnvironmentStore.CurrentDefinition` -
  the `Environments:Definitions` configuration entry for whichever environment (Testing, Staging,
  Production, Local) is currently selected via `Features/Environments/`. Switching environments changes
  which services and locations the picker and the OpenAPI fetch use on the very next call, with no
  caching. A service whose `Location` is blank for the current environment is skipped with a warning
  rather than offered with a broken link.

- **Swagger UI itself is a third-party bundle, loaded lazily and pointed at our endpoint, not the spec
  content.** `swagger-page.ts` passes `url: this.servicesApi.getOpenApiUrl(serviceName)` into
  `SwaggerUIBundle`, so Swagger UI itself performs the fetch (against this backend, same-origin) and
  parses the document; the component does not pre-fetch or hand it JSON directly. The bundle's JS/CSS
  ship as `inject: false` Angular CLI asset bundles (`angular.json`), loaded on demand by the shared
  `ScriptLoader`/`StyleLoader` helpers, consistent with how other heavy third-party UI is handled in
  this app.

## Risks / Trade-offs

- [Risk] A developer reads the on-page CORS notice as "Execute is broken" rather than "expected" →
  Mitigation: the notice in `swagger-page.html` states the limitation and the workaround directly; this
  spec records the same as a documented, accepted limitation rather than a defect.
- [Risk] A new Numo service that does not serve `swagger/v1/swagger.json` at that fixed path will
  simply fail to load, with no per-service override mechanism → Mitigation: this is accepted today
  (comment in `GetServiceOpenApiHandler`); every current Numo service follows the convention.
- [Trade-off] The narrow single-path endpoint means any future "fetch a different document from a
  service" need requires a new handler, not a parameter - consistent with the repo-wide
  "narrow endpoint per need" convention, traded deliberately against proxy flexibility for network
  safety.

## Migration Plan

Not applicable - no code changes. This design doc is retroactive documentation of the shipped feature.

## Open Questions

None specific to this capability. The repo-wide open question about the service registry eventually
moving into the database (`docs/Purpose.md`, "Open questions") applies here as it does to every other
consumer of `IServiceDiscoveryService`, but is not a question about this capability specifically.
