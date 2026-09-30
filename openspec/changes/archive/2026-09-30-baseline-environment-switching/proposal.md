## Why

The `Features/Environments/` slice is already implemented and shipped, but `docs/Purpose.md` predates it and OpenSpec has no record of it. This change does not propose new work - it retroactively documents the already-built environment-switching capability from the real code, so OpenSpec has an accurate baseline spec to diff future changes against.

## What Changes

- Document the existing set of configured environments (Testing, Staging, Production, Local) read from the `Environments` configuration section in `appsettings.json`.
- Document how the active environment is tracked (`CurrentEnvironmentStore`, in-memory, reset to the configured default on restart, no persistence).
- Document the API surface for reading and switching the active environment (`GET /api/environments`, `PUT /api/environments/current`).
- Document the replacement of `IServiceDiscoveryService`'s default `ConfigurationServiceDiscoveryService` with `EnvironmentAwareServiceDiscovery`, which resolves a service's location from the currently active environment's own `Services` map instead of a flat section.
- Document the frontend environment picker in the app layout header, and that switching reloads the page rather than invalidating per-page caches.
- Document failure behavior for an unconfigured service or an unknown environment key.

## Capabilities

### New Capabilities
- `environment-switching`: tracking which deployment environment is active, exposing it to the frontend, allowing it to be switched at runtime, and resolving Numo service locations against the active environment instead of a flat configuration section.

### Modified Capabilities
(none - no existing specs exist yet for this area)

## Impact

- No code changes. This is documentation-only, covering `src/Numo.DevServices.Api/Features/Environments/*`, its `Environments` configuration section in `appsettings.json`, and the frontend environment picker in `ClientApp/src/app/core/environments/` and `ClientApp/src/app/core/layout/app-layout.*`.
