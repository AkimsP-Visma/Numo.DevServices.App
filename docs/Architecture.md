# Architecture

## Shape

A single ASP.NET Core project holds the whole backend. Code is organised by feature, not by
technical layer: everything a slice needs - entity, EF mapping, validation, handlers, controller -
sits in `Features/<Slice>/`. Adding a feature means adding a folder; it does not mean touching
four projects.

```
src/Numo.DevServices.Api/
  Program.cs                  numo-core host
  DevServicesModule.cs        IBusinessModule and assembly marker
  Features/SampleItems/       the reference slice
    SampleItem.cs                     entity : BaseEntity
    SampleItemSpecification.cs        field constraints, single source
    SampleItemConfiguration.cs        EF mapping
    SampleItemErrors.cs               NumoErrors with stable ids
    GetSampleItems.cs                 query + validator + result + handler
    GetSampleItemById.cs
    CreateSampleItem.cs
    SampleItemsController.cs
  Persistence/
    DevServicesDbContext.cs           schema "DevServices"
    DevServicesDbContextDesignFactory.cs
    Migrations/
  ClientApp/                  Angular 21
```

## Framework

numo-core 7.0.248 (`Numo.Core.*`) over AppForeach. `Program.cs` builds the host with
`NumoWebApplication.CreateBuilder(args)` and registers four features:

| Call | Effect |
| --- | --- |
| `AddNumo<DevServicesModule>()` | runs the module's own service registrations |
| `AddNumoMediator<DevServicesModule>()` | discovers `*Handler` and validators in this assembly |
| `AddNumoWebApi<DevServicesModule>()` | controllers plus the global `NumoResult` result filter |
| `AddNumoSql<DevServicesDbContext>()` | Npgsql on `DefaultConnection`, migration startup, `IUnitOfWork` |

## Request flow

Controller receives the HTTP request and dispatches through `INumoMediator`. The pipeline runs the
request's FluentValidation validator, then the handler, which queries or mutates
`DevServicesDbContext` and returns a `NumoResult<T>`. The global result filter unwraps a success to
its value and converts a failure - a validation error or a `NumoError` the handler returned - into
an RFC 7807 problem-details response with `errorId` and `errors` extensions.

## Origin

Modelled on `Numo.WorkTimePlanning.App`, which contains two backend styles: `Numo.WorkTimePlanning
.Modules` (vertical slices on MediatR and FluentResults) and `Numo.TimeTracking.*` (four layered
projects on numo-core). This solution takes the slice layout from the first and the framework from
the second.

Three deliberate departures from the `Numo.TimeTracking.*` module:

1. **One project.** Splitting a slice across Domain, Application, Infrastructure, and Presentation
   is what makes that module layered rather than vertical.
2. **No repository abstraction.** Handlers use the `DbContext` directly. Within a single project a
   repository interface adds indirection without isolation. The cost is that handler tests need a
   real or SQLite-backed context rather than a mock.
3. **`BaseEntity`, not `BaseTenantEntity`.** The reference's tenant query filter reads
   `INumoCurrentTenantService`, which needs an authenticated principal; with authentication off it
   would match `Guid.Empty` for every row. Introducing tenancy later is a base-class change plus a
   migration.

The host also uses the real numo-core builder rather than the reference's
`RunNumoCoreFeatureInstallers` workaround, which exists only because that host predates it.

## Frontend

Angular 21 standalone application in `ClientApp`, served under the base href
`/app/dev-services/`. Routes lazy-load one feature each. The sample page holds its state in
signals and talks to a feature-local API service; PrimeNG supplies the components and
`@vismaux/vud` the Visma styling.

In development `ng serve` proxies `/app/dev-services/api/*` to the API host and strips the
prefix. The API host does not serve the SPA - neither does the reference host - so production
hosting for the built bundle is still to be decided.

## Not wired yet

- **Authentication.** The platform moved to Keycloak; nothing is configured and every endpoint is
  open. Expect to add authentication to the host, `[Authorize]` to controllers, tenancy to
  entities, and a token interceptor to the frontend as one piece of work.
- **Tests, Docker Compose, CI, i18n.**
