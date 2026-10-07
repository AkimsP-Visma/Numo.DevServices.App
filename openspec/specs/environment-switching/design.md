# environment-switching design

## How it works
Every consumer in the app reaches other services through `IServiceDiscoveryService`. This
capability replaces that interface's registration with an environment-aware implementation, which
looks up the service in whichever environment (Testing, Staging, Production, Local) is currently
selected. The selection is held in memory, starts at the configured default, and is changed through
`PUT /api/environments/current`. The frontend reloads the whole page after a switch.

## Decisions
- **Replace the discovery registration instead of wrapping it.** The library's own implementation
  is stateless, reads one flat `Services` section, and has no way to hold several configurations,
  so a decorator would still need a shape it doesn't have. Every caller depends on the interface,
  so replacing the registration makes all of them environment-aware without changing any of them.
- **Look up the current environment on every call, with no caching.** That is what makes a switch
  visible on the very next request. Each environment's `Services` entries keep the same shape as
  the flat section (`Location`, `AppId`), so they are a drop-in per-environment copy.
- **The selection lives only for the process.** A restart resets it to the default. Re-picking
  after a restart is cheaper for a single-developer tool than persisting the choice in a settings
  table, a migration or a cookie.
- **Bad input fails with stable errors, not silently.** An unknown environment key fails with
  `EnvironmentsErrors.UnknownEnvironment`. A service missing from the active environment, or with a
  blank location, throws the library's own `ServiceDoesNotExistException`, exactly as the original
  implementation does for a missing key, so callers written against it need no change.
- **A switch reloads the page rather than invalidating state per component.** Everything fetched
  for the old environment is discarded at once, so no component can show data from the wrong one.

## Constraints and limitations
- `GetServiceAppId` always throws, because no environment defines an `AppId`.
- An environment can define only some of the services (Local omits some). A lookup for a missing
  one throws, the same way a missing service does anywhere else.
