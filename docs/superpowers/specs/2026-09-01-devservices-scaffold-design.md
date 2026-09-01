# Numo.DevServices.App - Scaffold Design

**Date:** 2026-09-01
**Status:** Approved

## Goal

Create a working greenfield scaffold: a .NET 10 backend organised in vertical slices, an
Angular 21 frontend, one sample slice reaching from an HTTP endpoint through to Postgres,
and one sample page rendering it.

## Origin and departures from Numo.WorkTimePlanning.App

The reference solution contains two backend styles:

- `Numo.WorkTimePlanning.Modules` - vertical slices. One `Actions/<Action>.cs` file holds the
  request record, its `AbstractValidator`, and the handler. Built on MediatR and FluentResults.
- `Numo.TimeTracking.*` - four layered projects (Domain, Application, Infrastructure,
  Presentation) with feature folders inside each. Built on numo-core.

This scaffold takes the slice layout from the first and the framework from the second.

Three deliberate departures:

1. **One backend project.** Slices live in `Features/<Slice>/` inside `Numo.DevServices.Api`.
   Splitting a slice across four projects is what makes the reference's TimeTracking module
   layered rather than vertical.
2. **No repository abstraction.** Handlers inject `DevServicesDbContext` directly. Within a
   single project a repository interface adds indirection without isolation. Cost: handler
   tests need a real or SQLite-backed context rather than a mock.
3. **`BaseEntity`, not `BaseTenantEntity`.** The reference's tenant query filter reads
   `INumoCurrentTenantService`, which needs an authenticated principal. With authentication
   off, a tenant filter would match `Guid.Empty` for every row. Introducing tenancy later is
   a base-class change plus a migration.

The scaffold also uses the real numo-core host (`NumoWebApplication.CreateBuilder`) instead of
the reference's `RunNumoCoreFeatureInstallers` workaround, whose own comment records that it
exists only because that host predates the builder.

## Out of scope

Authentication (the platform moved to Keycloak; wiring it is separate work), Docker Compose,
CI, a test project, Transloco i18n, and production SPA hosting. The reference host does not
serve its own SPA either - there is no static-file or SPA middleware in its `Startup.cs`.

## Backend

```
src/Numo.DevServices.Api/            Microsoft.NET.Sdk.Web, net10.0
  Program.cs                         NumoWebApplication.CreateBuilder(args)
  DevServicesModule.cs               IBusinessModule; also the mediator assembly marker
  GlobalUsings.cs
  Features/SampleItems/
    SampleItemsController.cs
    SampleItem.cs                    entity : BaseEntity
    SampleItemConfiguration.cs       EF mapping : BaseEntityConfiguration<SampleItem>
    GetSampleItems.cs                query + validator + result + handler
    GetSampleItemById.cs
    CreateSampleItem.cs
  Persistence/
    DevServicesDbContext.cs          schema "DevServices"
    DevServicesDbContextDesignFactory.cs
    Migrations/
  appsettings.json, appsettings.Development.json
```

### Packages

`Numo.Core.Application`, `Numo.Core.Domain`, `Numo.Core.Infrastructure`,
`Numo.Core.Host.AspNetCore` (all 7.0.248), `Microsoft.EntityFrameworkCore` and
`.Design` 10.0.10, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2. They resolve from the
`Visma-Horizon-4` GitHub feed, which is already configured machine-wide.

### Host wiring

`NumoWebApplicationBuilder` inherits public `ConfigureServices(Action<IServiceCollection,
IConfiguration>)`, `ConfigureWeb(Action<WebApplication>)`, and `Run()`, so `Program.cs` needs no
subclass:

```csharp
var builder = NumoWebApplication.CreateBuilder(args);
builder.ConfigureServices((services, _) =>
{
    services.AddNumo<DevServicesModule>();
    services.AddNumoMediator<DevServicesModule>();   // scans this assembly for *Handler
    services.AddNumoWebApi<DevServicesModule>(_ => { });
    services.AddNumoSql<DevServicesDbContext>();     // Npgsql, migrations, IUnitOfWork
    services.AddControllers();
    services.AddCors(...);                            // dev origin for ng serve
});
builder.ConfigureWeb(app => { app.UseCors(); app.MapControllers(); });
builder.Run();
```

`AddNumoWebApi` registers the framework's own `TransformNumoResultAttribute` (internal to
`Numo.Core.Host.AspNetCore`) as a global filter, so controllers do not need the public copy the
reference keeps in `Numo.TimeTracking.Presentation/Filters`.

### Slice shape

Each action file carries request, validator, result, and handler. Handlers are plain classes
named `<Action>Handler` with `HandleAsync(<Action>Query|Command, CancellationToken)` returning
`NumoResult<T>` - the convention `AddNumoMediator` discovers. Controllers hold no logic beyond
dispatching through `INumoMediator`.

Routes are `api/<kebab-case-plural>`. The `/app/dev-services` prefix belongs to the frontend base
href and is stripped by the proxy, so the API host serves plain routes - what the reference's own
comment says a standalone numo-core service should do.

### Data flow and errors

Controller -> `INumoMediator.SendAsync<T>` -> FluentValidation in the pipeline -> handler ->
`DbContext` -> `NumoResult.Ok(...)`. The global result filter turns a failed `NumoResult` into an
RFC 7807 problem-details response and unwraps a successful one to its value. `DevServicesDbContext`
implements `IUnitOfWork`, and writing handlers commit through it explicitly, as the reference's
command handlers do.

### Database

Postgres, schema `DevServices`, one initial migration creating `SampleItems`.
`AddNumoSql<TDbContext>` reads the `DefaultConnection` connection string and keeps migration
history in `__EFMigrationsHistory`. `appsettings.Development.json` points at the local instance
already used by the reference (`localhost:5432`, `hor4_db_user`) but a separate
`devservices_db` database. The migration startup applies migrations on boot.

## Frontend

Angular 21 standalone in `src/Numo.DevServices.Api/ClientApp`, `baseHref: /app/dev-services/`,
`proxy.conf.js` forwarding `/app/dev-services/api` to the .NET host with the same path rewrite
the reference uses.

```
src/app/
  app.config.ts, app.routes.ts, app.ts
  core/layout/                     shell with nav
  shared/api/                      PaginatedList and problem-details response models
  features/sample-items/
    sample-items.routes.ts
    api/sample-items-api.service.ts, sample-item.model.ts
    pages/sample-items-page/
```

The sample page lists items in a PrimeNG table bound to a signal and creates one through a
dialog, then reloads. Signals and `inject()`, `OnPush`, `@if`/`@for`.

Dependencies: PrimeNG 21 with `@primeuix/themes`, plus `@vismaux/vud` for the Visma look.
`@vismaux/vud-icons` is left out: its stylesheet and the VUD one emit images with identical
names, which the bundler rejects, and VUD already carries the icons. For the same reason the
development build sets `outputHashing: "media"`, and the initial-bundle budget is raised to
accommodate the 1.3 MB VUD stylesheet. `@visma-horizon-4/ngx-common` is excluded: it is the Horizon platform
integration layer (auth, gateway, app shell) and cannot function with authentication off.

## Documentation

`CLAUDE.md` records the slice conventions and run commands. `docs/Architecture.md` describes the
structure and the three departures above.

## Verification

The scaffold is done when `dotnet build` succeeds, the API starts and serves the sample endpoint,
`npm run build` succeeds, and the sample page lists and creates items against the running API.
