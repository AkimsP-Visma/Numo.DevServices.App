## Why

`api-catalog-browser` (the "Swagger UI for multiple services" feature described as Feature 1 in
`docs/Purpose.md`) is already built and shipped: `Features/Services/` on the backend and the
`/swagger` page on the frontend. OpenSpec was only just initialized in this repository, so no
capability spec exists yet for work that predates it. This change adds no behavior; it retroactively
documents the feature exactly as implemented today, so OpenSpec has a baseline spec to diff future
changes against instead of starting from nothing.

## What Changes

- Document the existing `GET api/services` endpoint that lists the Numo services registered for the
  currently active environment.
- Document the existing `GET api/services/{serviceName}/openapi` endpoint that fetches a named
  service's OpenAPI document server-side, rewrites it to point "Try it out" at the service's real
  location, and returns it - never forwarding an arbitrary path, only this one fixed document path.
- Document the existing `/swagger` frontend page: a service picker backed by the services list, and
  a Swagger UI instance rendered against the rewritten document, including the on-page notice that
  "Try it out" needs a CORS-disabling extension because Numo services send no CORS headers.
- No code changes accompany this proposal.

## Capabilities

### New Capabilities
- `api-catalog-browser`: server-side OpenAPI document fetching/rewriting and the multi-service
  Swagger UI picker page that renders it.

### Modified Capabilities
(none)

## Impact

- Affected code (read-only, documented as-is): `src/Numo.DevServices.Api/Features/Services/*`,
  `src/Numo.DevServices.Api/ClientApp/src/app/features/services/*`, the `Environments` configuration
  section read through `IServiceDiscoveryService`.
- No APIs, dependencies, or schemas change as part of this proposal.
