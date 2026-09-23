# Three tasks: remove Sample Items, a run-locally bat file, and an environment selector

## 1. Remove Sample Items

`Features/SampleItems/` (backend) and `ClientApp/src/app/features/sample-items/` (frontend) are the
scaffolding slice `CLAUDE.md`, `docs/Purpose.md` and `docs/Architecture.md` all describe as disposable
once real features land - they have. This is a straight removal, not a design decision.

**Touch points:**
- Delete `Features/SampleItems/` and its `ClientApp` counterpart entirely.
- Remove the nav entry (`app-layout.ts`) and the route (`app.routes.ts`).
- Remove `DbSet<SampleItem>` from `DevServicesDbContext`, plus a new EF migration dropping the table
  (keeps migration history honest instead of leaving orphaned schema).
- Update the three docs that describe it as the copy-this-shape template.

## 2. `Run-DevServices.bat`

A batch file at the repo root for someone without Rider or a terminal habit to start both halves of
the app:

1. Check `src/Numo.DevServices.Api/appsettings.Development.json` exists; if not, copy it from
   `appsettings.Development.Template.json` and print a message that Postgres needs to be running on
   `localhost:5432` with the credentials in that file.
2. Start the backend (`dotnet run --project src\Numo.DevServices.Api`) in its own visible window
   (`start "Numo Dev Services - API" cmd /k ...`), so errors are readable instead of a window
   flashing shut.
3. Start the frontend (`npm run serve` inside `ClientApp`, running `npm install` first if
   `node_modules` is missing) in its own window the same way.
4. After a short wait, open the default browser to `http://localhost:4200/app/dev-services/`.

No environment prompt in the bat file itself - environment switching is a live, in-app dropdown (see
below), not a launch-time choice.

## 3. Environment selector

### Problem

The backend's `Services` config section is a single flat list of URLs (all currently pointing at
`test.numo.lv`). Every downstream call - the Personnel/DataIntegration browsers, the service-health
ping dashboard, and the Swagger doc viewer - goes through whichever URL is configured, with no way to
point them at a different environment without editing config and restarting.

### How it's actually consumed today (verified, not assumed)

- `IServiceDiscoveryService` (`Numo.Common.Lib.ServiceDiscovery.ConfigurationServiceDiscoveryService`)
  re-reads `IConfiguration`'s `Services` section on every call - nothing caches it.
- It's registered with `services.TryAddSingleton<IServiceDiscoveryService, ConfigurationServiceDiscoveryService>()`
  in `AddNumoCommonServices()` - a `TryAdd`, meaning a later `services.Replace(...)` for the same
  interface cleanly swaps the implementation, no fork of the shared library needed.
- Every call site in this project depends on the *interface*, not the concrete type: `ServiceDataRegistration`,
  `GetServiceHealth`, `GetServiceOpenApi`, `GetServices` (5 call sites in total, listed for completeness -
  see the plan). None of them need to change.

This means an environment-aware *replacement* implementation of `IServiceDiscoveryService`, swapped in
after `AddNumoCommonServices()`, makes every existing consumer environment-aware for free.

### Config shape

Replaces the flat `Services` section with `Environments`, still plain URLs (no secrets), still safe to
commit:

```json
"Environments": {
  "Default": "Testing",
  "Definitions": {
    "Testing": {
      "Services": {
        "Numo.Authorization.Api": { "Location": "https://test.numo.lv/authorization-api/" },
        "Numo.DataIntegration.Configuration.Api": { "Location": "https://test.numo.lv/dataintegration-configuration-api/" },
        "Numo.DataIntegration.Connectors.Api": { "Location": "https://test.numo.lv/dataintegration-connectors-api/" },
        "Numo.Employee.Api": { "Location": "https://test.numo.lv/employee-api/" },
        "Numo.Person.Api": { "Location": "https://test.numo.lv/person-api/" }
      }
    },
    "Staging": {
      "Services": {
        "Numo.Authorization.Api": { "Location": "https://stage.numo.lv/authorization-api/" },
        "Numo.DataIntegration.Configuration.Api": { "Location": "https://stage.numo.lv/dataintegration-configuration-api/" },
        "Numo.DataIntegration.Connectors.Api": { "Location": "https://stage.numo.lv/dataintegration-connectors-api/" },
        "Numo.Employee.Api": { "Location": "https://stage.numo.lv/employee-api/" },
        "Numo.Person.Api": { "Location": "https://stage.numo.lv/person-api/" }
      }
    },
    "Production": {
      "Services": {
        "Numo.Authorization.Api": { "Location": "https://app.numo.lv/authorization-api/" },
        "Numo.DataIntegration.Configuration.Api": { "Location": "https://app.numo.lv/dataintegration-configuration-api/" },
        "Numo.DataIntegration.Connectors.Api": { "Location": "https://app.numo.lv/dataintegration-connectors-api/" },
        "Numo.Employee.Api": { "Location": "https://app.numo.lv/employee-api/" },
        "Numo.Person.Api": { "Location": "https://app.numo.lv/person-api/" }
      }
    },
    "Local": {
      "Services": {
        "Numo.DataIntegration.Configuration.Api": { "Location": "https://localhost:7291" },
        "Numo.DataIntegration.Connectors.Api": { "Location": "https://localhost:7176" },
        "Numo.Employee.Api": { "Location": "https://localhost:7053" },
        "Numo.Person.Api": { "Location": "https://localhost:7162" },
        "Numo.Landing.App": { "Location": "http://localhost:5031" },
        "Numo.WorkTimePlanning.App": { "Location": "https://localhost:7266" },
        "Numo.WorkTime.App": { "Location": "https://localhost:7266" },
        "Numo.Workflow.Configuration.App": { "Location": "http://localhost:44800" },
        "Numo.Workflow.Task.App": { "Location": "http://localhost:44801" }
      }
    }
  }
}
```

`Numo.Authorization.Api` has no `Local` entry (its local port is unknown) - an environment is free to
list a different set of service keys than another; the ping dashboard and Swagger picker simply show
whatever the active environment defines. `Numo.Landing.App`, `Numo.WorkTimePlanning.App`,
`Numo.WorkTime.App` and the two `Numo.Workflow.*` apps exist **only** under `Local` - they're for
pinging and viewing Swagger docs while developing against apps run locally, not resources this tool's
Personnel/DataIntegration browsers read from, and have no Testing/Staging/Production entry.

This lives in the *committed* `appsettings.json` (not the gitignored `Development` file) since none of
it is secret - it's the same plain hostnames already committed in today's template, just three sets
instead of one, plus the always-local `Local` set.

### Backend: `Features/Environments/`

- **`EnvironmentsOptions`** - binds the `Environments` section: `Default: string`,
  `Definitions: Dictionary<string, EnvironmentDefinition>`, where `EnvironmentDefinition` has
  `Services: Dictionary<string, ServiceLocationDefinition>` and `ServiceLocationDefinition` has
  `Location`/`AppId` (mirrors today's flat shape, just nested one level deeper).
- **`CurrentEnvironmentStore`** - an in-memory singleton holding the active environment key, seeded
  from `Default` at startup, reset on every app restart (matches the frontend `TenantIdStore`'s own
  ephemeral, non-persisted philosophy - no new persistence mechanism to design or get wrong). Guards
  `Set(key)` against unknown keys.
- **`EnvironmentAwareServiceDiscovery : IServiceDiscoveryService`** - reads
  `Definitions[current].Services` instead of a flat section; `GetAllServices()`/`GetServiceLocation`
  behave like the original for a known key, throw the same `ServiceDoesNotExistException` for an
  unknown one. `GetServiceAppId` keeps throwing, same as today (no `AppId` anywhere in our config, by
  design, per the existing `Numo.Common.Lib` convention note in `CLAUDE.md`).
- Registered via `services.Replace(ServiceDescriptor.Singleton<IServiceDiscoveryService, EnvironmentAwareServiceDiscovery>())`
  in `DevServicesModule.ConfigureServices`, after `AddNumoCommonServices()`.
- Two actions: `GetCurrentEnvironment` (current key + the known list, for the frontend selector to
  populate itself) and `SetCurrentEnvironment` (validates against the known list, updates the store).
  A small `EnvironmentsController` and `EnvironmentsErrors` (`UnknownEnvironment`), following the same
  one-file-per-action convention as every other slice.

**Scope note:** this only affects the Personnel/DataIntegration browsers, the service-health ping
dashboard, and the Swagger doc viewer - *not* the Feature Toggles page, whose "facade/testing/staging/production"
columns come from LaunchDarkly's own environments and are unrelated to this.

### Frontend: selector + environment-driven chrome

- A small `EnvironmentsApiService` (`GET`/`PUT` against the new endpoints) and a signal-backed store,
  loaded once at app startup (similar shape to `TenantIdStore`, but this one is global rather than
  scoped to the service-data feature, so it lives under `core/`).
- A compact selector in the app header (`app-layout.html`), next to the title - global rather than
  per-page, since it changes what every page's data comes from. On change: call the endpoint, then
  `window.location.reload()` so no page is left showing data fetched under the old environment (simple
  and correct; no per-page cache-invalidation logic to get subtly wrong).
- **Header/desktop color reflects the active environment**, reusing the exact accent colors already
  established for tags/messages rather than inventing new ones:
  - Testing (default): `#123B12` → `#0A7D0A` (same green as an "On" tag)
  - Staging: `#5C3D00` → `#C77F00` (same amber as a warning)
  - Production: `#591111` → `#B22222` (same red as "Off"/danger)
  - Local: kept as the existing Luna blue gradient (no new color needed - it's the "nothing special,
    just my machine" case)

  This changes only the title-bar gradient and the desktop dither strip's two tones - buttons, links
  and selection highlighting stay Luna blue everywhere regardless of environment, since those aren't
  about *which* environment you're in.
- Implementation: a CSS class on the app shell root (`app-layout--testing` / `--staging` / `--production` / `--local`)
  driven by the loaded environment signal, each defining the two gradient-stop custom properties the
  header and desktop rules already read from.

### Testing

No test project exists in this repo (`CLAUDE.md`: tests are deliberately absent). Verification is
manual: run the app, confirm the selector lists all four environments, confirm switching actually
changes ping dashboard results and Swagger's fetched spec, confirm header color changes, confirm an
unknown environment key is rejected server-side.
