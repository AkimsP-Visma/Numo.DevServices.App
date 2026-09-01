# Numo.DevServices.App

.NET 10 API in vertical slices with an Angular 21 frontend. `SampleItems` is a placeholder
slice proving the pipeline end to end - copy its shape, then delete it once real features land.

## Layout

```
src/Numo.DevServices.Api/          the whole backend, one project
  Program.cs                       numo-core host wiring
  DevServicesModule.cs             IBusinessModule; also the assembly marker for handler discovery
  GlobalUsings.cs
  Features/<Slice>/                one folder per slice, everything it needs inside
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
- **Handlers take `DevServicesDbContext` directly.** No repository interfaces: inside a single
  project they add indirection without isolation.
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

## Deliberately absent

Authentication (the platform uses Keycloak; nothing is wired yet, every endpoint is open),
tenancy (`BaseEntity`, not `BaseTenantEntity` - a tenant filter needs an authenticated
principal), tests, Docker, CI, i18n, and production SPA hosting. See `docs/Architecture.md`.
