# Architecture

What this app is for, and what is planned, is in `Purpose.md`. This document describes only the
shape of what exists today.

## Shape

A single ASP.NET Core project holds the whole backend. Code is organised by feature, not by
technical layer: everything a slice needs - entity, EF mapping, validation, handlers, controller -
sits in `Features/<Slice>/`. Adding a feature means adding a folder; it does not mean touching
four projects.

```
src/Numo.DevServices.Api/
  Program.cs                  numo-core host
  DevServicesModule.cs        IBusinessModule and assembly marker
  Features/Services/          the registry of Numo services and their OpenAPI documents
  Features/FeatureFlags/      the LaunchDarkly toggle list
  Features/ServiceHealth/     the ping dashboard over the configured services
  Features/ServiceData/       descriptor-driven browsing across Personnel and DataIntegration
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

   `Features/ServiceData/` now uses that same interface, which is not a reversal of this decision.
   The two are different things: there it supplies a tenant id to *downstream* Person and Employee
   calls, set per request from a header through the supported `AddNumoTenantSetter` seam, and this
   app's own entities remain untenanted. Nothing reads a tenant from a principal, because there is
   still no principal to read.

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

- **Authentication.** Nothing is configured and every endpoint is open. Expect to add authentication
  to the host, `[Authorize]` to controllers, tenancy to entities, and a token interceptor to the
  frontend as one piece of work.

  Note the platform itself uses Microsoft Identity Web, not Keycloak as earlier drafts of this
  document said: `Numo.Authentication.Lib` exposes `AddMicrosoftIdentityWebApiAuthentication`,
  `EnableTokenAcquisitionToCallDownstreamApi` and `AddDistributedTokenCaches`.

  **`Features/ServiceData/` is the first slice that must gain `[Authorize]`**, and the reason is
  worth stating plainly. It reads personal data - names, emails, phones and person codes - for
  whatever tenant id a caller types into the browser, with no credential of any kind, because the
  Person and Employee services enforce nothing beyond that header's presence. That is acceptable
  only while this app is network-internal and developer-only. `docs/Purpose.md`'s standing decision
  against a pass-through route was written to keep this app from becoming a hole into the internal
  network; that risk arrives through this slice by the data path rather than the path path, so the
  decision is recorded here rather than left implicit.

  The DataIntegration resources added later to the same slice carry no personal data and need no
  tenant header at all, so they do not add to that specific risk - except for three resources that
  read real, sensitive payloads rather than configuration: `di-connection-credentials` and
  `di-connection-certificates` (plaintext credential values, certificate material), and
  `di-execution-step-dataset` (the actual records a pipeline moved - confirmed live to include
  HR/absence data tied to individual people for at least one real pipeline, which is exactly the
  kind of personal data the tenant-header risk above is about, arriving by a different route). All
  three are reachable only via a relation button, never listed, and fetched only on demand, which
  is the mitigation available today; `[Authorize]` closes the actual gap.

- **A dependency hazard worth knowing about.** The `positions` detail page fans out concurrently,
  and the tenant override reaching those parallel calls depends on `AddNumoTenantSetter` storing it
  in an `AsyncLocal`, which flows into child tasks. That is `AsyncLocal`'s own documented behaviour,
  so it is safe today; but if a future `Numo.Common.Microservice.Lib` switched to request-scoped
  storage, the symptom would be wrong-tenant data rather than an error. Worth re-probing on a major
  bump of that package.
- **Tests, Docker Compose, CI, i18n.**
