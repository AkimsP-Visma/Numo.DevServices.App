## Context

DevServices reads every other Numo service's location through `IServiceDiscoveryService` from `Numo.Common.Lib`, whose default `ConfigurationServiceDiscoveryService` implementation reads a flat `Services` configuration section. As a developer tool that a user points at Testing, Staging, Production, or a locally-running set of services, DevServices needs the *same* set of service ids to resolve to a *different* location depending on which environment the developer is currently working against - the default implementation has no concept of that. This is a retroactive design writeup for the already-shipped `Features/Environments/` slice; no code changes accompany it.

## Goals / Non-Goals

**Goals:**
- Record why `IServiceDiscoveryService` was replaced wholesale rather than extended.
- Record what `EnvironmentAwareServiceDiscovery` does differently from `ConfigurationServiceDiscoveryService`.
- Record how the active environment is selected, changed, and persisted (or not).

**Non-Goals:**
- Changing any of this behavior.
- Covering authentication/authorization for the switch endpoint (deliberately absent project-wide, per root `CLAUDE.md`).

## Decisions

- **Replace `IServiceDiscoveryService` via `services.Replace`, not extend or wrap it.** `ConfigurationServiceDiscoveryService` is registered by `AddNumoCommonServices` with `TryAddSingleton` and is stateless: it reads a flat `Services` section once and has no hook for "which environment." Every existing and future caller (`ServiceDataRegistration`, `GetServiceHealth`, `GetServiceOpenApi`, `GetServices`, and each of the 19 `IServiceDataResource` implementations) already depends on the `IServiceDiscoveryService` interface, never the concrete type, so swapping the registration in `EnvironmentsRegistration.AddEnvironmentsFeature` (called after `AddNumoCommonServices`) makes all of them environment-aware for free, with zero changes to any of them. A decorator wrapping the original would still need the original to hold multiple configurations at once, which its constructor shape doesn't support; replacing it outright is simpler than retrofitting that shape onto a type this codebase doesn't own.

- **`EnvironmentAwareServiceDiscovery` reads from `CurrentEnvironmentStore.CurrentDefinition` on every call, not from `IOptions<EnvironmentsOptions>` directly.** This is what makes an environment switch visible on the very next request: `GetServiceLocation`/`GetAllServices` do no caching of their own, they just index into whichever `EnvironmentDefinition` is current at call time. Mirrors the original's `ServiceLocationDefinition` shape (`Location` + `AppId`) so the `Environments:<Env>:Services:<id>` entries are a drop-in per-environment copy of what the old flat `Services` section held. `GetServiceAppId` still always throws `ServiceDoesNotExistException`, matching the existing project convention that no environment defines an `AppId`.

- **The active environment is an in-memory, process-lifetime value (`CurrentEnvironmentStore`), not a configuration value, a cookie, or a database row.** It starts at `EnvironmentsOptions.Default` ("Testing" in `appsettings.json`) and can be changed at runtime through `PUT /api/environments/current`, but a restart resets it to the default again. This is a deliberate no-persistence choice, the same philosophy the code comments compare to the frontend's `TenantIdStore`: a single-user developer tool where re-picking an environment after a restart is cheaper than the added complexity of persisting a choice (a migration, a settings table, or a cookie) for a value nobody needs to survive a restart.

- **Unknown environment keys and unconfigured services fail as stable, branchable errors, not silently.** `SetEnvironmentHandler` checks membership through `CurrentEnvironmentStore.TrySet`, which returns `false` for a key absent from `EnvironmentsOptions.Definitions`; the handler turns that into `EnvironmentsErrors.UnknownEnvironment`, a `NumoError` with a fixed id, rather than throwing or silently ignoring the request. A service id absent from the active environment's `Services` map (or present with a blank `Location`) surfaces as `Numo.Common.Lib`'s own `ServiceDoesNotExistException` from `GetServiceLocation`, matching how the original `ConfigurationServiceDiscoveryService` behaves for a missing key - callers written against that exception need no change.

## Risks / Trade-offs

- [No persistence across restarts] -> Accepted: this is a single-developer local tool: a restart already discards the frontend's per-session state, so the environment choice resetting alongside it is consistent, not surprising.
- [Switching does not invalidate already-fetched frontend data] -> Mitigated by the frontend reloading the whole page (`window.location.reload()`) on switch rather than trying to invalidate caches per-component.
- [An environment can define a partial service set, e.g. `Local` omits `Numo.Authorization.Api`] -> Accepted: surfaces as the existing `ServiceDoesNotExistException` for that specific lookup, same as a genuinely missing service today.

## Open Questions

None - this is a documentation-only baseline for already-shipped behavior.
