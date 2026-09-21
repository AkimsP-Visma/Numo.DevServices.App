# Numo.DevServices.App

.NET 10 API in vertical slices with an Angular 21 frontend. `SampleItems` is a placeholder
slice proving the pipeline end to end - copy its shape, then delete it once real features land.

A developer-only tool for browsing data the main Numo UI does not expose. **Read
`docs/Purpose.md` first** - it holds the planned features, the standing design decisions
(frontend calls this backend only, prefer service client libs, generic data-driven UI), and the
open questions. Nothing real is implemented yet.

## Layout

```
src/Numo.DevServices.Api/          the whole backend, one project
  Program.cs                       numo-core host wiring
  DevServicesModule.cs             IBusinessModule; also the assembly marker for handler discovery
  GlobalUsings.cs
  Features/<Slice>/                one folder per slice, everything it needs inside
  Features/Services/               the registry of Numo services and their OpenAPI documents
  Features/FeatureFlags/           the LaunchDarkly toggle list, read over the LaunchDarkly REST API
  Features/ServiceHealth/          the ping dashboard over the same configured services
  Persistence/                     DbContext, design-time factory, migrations
  ClientApp/                       Angular app
```

## Running it

`appsettings.Development.json` is gitignored: copy `appsettings.Development.Template.json` over it
once and adjust. It needs Postgres on `localhost:5432` with those credentials; `devservices_db` is
created and migrated on first boot.

```
dotnet run --project src/Numo.DevServices.Api        # https://localhost:7220, http://localhost:5220
cd src/Numo.DevServices.Api/ClientApp && npm run serve   # http://localhost:4200/app/dev-services
```

The dev server proxies `/app/dev-services/api/*` to the API host, so run both.

In Rider the same thing is `.run/`: **Api + Frontend** is a compound that starts both and stops
both, **Api** runs only the backend when you want to drive `ng serve` yourself, and
**Frontend (ng serve)** is the npm script on its own.

Adding a migration:

```
dotnet ef migrations add <Name> --project src/Numo.DevServices.Api --output-dir Persistence/Migrations
```

## Backend conventions

- **A slice owns its entity.** Entity, entity specification, EF configuration, actions, and
  controller all live in `Features/<Slice>/`. Nothing about a slice lives in a shared folder.
- **One file per action**, named after it: request record, `AbstractValidator`, result record,
  and handler together. `GetSampleItems.cs` is the model.
- **Handlers are plain classes** named `<Action>Handler` with
  `HandleAsync(<Action>Query|Command, CancellationToken)` returning `NumoResult<T>`.
  `AddNumoMediator<DevServicesModule>()` finds them by that convention - no interface, no manual
  registration. The same scan registers validators, which run in the pipeline before the handler.
- **Every request type needs a validator**, even a rule-less `AbstractValidator<T>;`. The mediator
  fails the request with a `FrameworkException` when it finds none, rather than skipping validation.
- **Handlers take `DevServicesDbContext` directly.** No repository interfaces: inside a single
  project they add indirection without isolation.
- **Reach other Numo services through their shared abstractions.** `IServiceDiscoveryService` from
  `Numo.Common.Lib` (registered by `AddNumoCommonServices`) reads the `Services` configuration
  section; a service's own client library is preferred over a hand-rolled HTTP client. Note that its
  lookups throw `ServiceDoesNotExistException` instead of returning null, and that `AppId` is absent
  from our configuration, so `GetServiceAppId` always throws.
- **A slice registers its own infrastructure** in a `<Slice>Registration.cs` extension the module
  calls, so an HTTP client or similar does not leak into `DevServicesModule`.
- **Register the module exactly once.** `AddNumoWebApi<DevServicesModule>()` already calls
  `AddNumo<DevServicesModule>()` internally, and `AddNumo` appends its `ModuleFeatureOption` with
  `AddSingleton`, so doing both makes numo-core construct the module twice and run
  `ConfigureServices` on both instances against the same collection - every `AddXFeature()` and
  everything inside it then happens twice. The template gets away with calling both because it
  passes a *different* module to each; a single-module service like this one must not. Prefer
  assignment over accumulation in a `ConfigureHttpClient` action anyway
  (`DefaultRequestHeaders.Authorization =`, not `TryAddWithoutValidation`), so a stray second
  registration degrades into a no-op instead of a duplicated header the remote end rejects.
- Two `PostgreSqlMigrationStartup` lines at startup are **not** a duplicate: they are the
  `DevServicesDbContext` and framework `FrameworkDbContext` tasks, and the log omits the generic
  argument that tells them apart.
- **Controllers only dispatch.** They take `INumoMediator`, build the request, and return
  `NumoResult<T>`; `AddNumoWebApi` unwraps a success to its value and turns a failure into an
  RFC 7807 problem-details response.
- **Failures are `NumoError`s with stable ids**, declared in one `<Slice>Errors.cs` per slice, so
  callers can branch on the id instead of the message.
- **Field constraints live in the entity specification.** The EF configuration derives column
  limits from it and validators derive rules with `RuleFor(...).From(specification, ...)`, so a
  length is written once.
- Routes are `api/<kebab-case-plural>`; the `/app/dev-services` prefix belongs to the frontend
  base href and is stripped by the proxy and the gateway.

## Frontend conventions

- Standalone components, signals, `inject()`, `OnPush`, `@if`/`@for`.
- One folder per feature under `src/app/features/<feature>/` with `api/`, `pages/`, and its own
  lazy-loaded routes file. Shared cross-feature code goes in `src/app/shared/`.
- API services return observables and live in the feature's `api/` folder; components hold state
  in signals.
- Paged endpoints need the nested query-string keys numo-core binds `PagedDataRequest` from -
  use `toPagedQueryParams`.
- Heavy third-party UI (Swagger UI) ships as lazy `styles`/`scripts` bundles in `angular.json` with
  `inject: false`, pulled in on demand by `ScriptLoader`/`StyleLoader`. Their URLs are relative to
  `<base href>`, not to the current route.

## Deliberately absent

Authentication (the platform uses Keycloak; nothing is wired yet, every endpoint is open),
tenancy (`BaseEntity`, not `BaseTenantEntity` - a tenant filter needs an authenticated
principal), tests, Docker, CI, i18n, and production SPA hosting. See `docs/Architecture.md`.
