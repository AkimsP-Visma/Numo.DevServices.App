# Purpose

An internal, developer-only tool. Real customers never reach it. It exists to make data and
configuration that the main Numo UI hides - or never surfaces at all - browsable during
development and support work.

Because the audience is developers, the usual product pressures do not apply: no i18n, no
polished empty states, no per-customer branding. Legibility and low effort to add the next
service win over anything else.

## Planned features

1. **Swagger UI for multiple services.** One place that renders any Numo service's OpenAPI
   spec, with a picker to switch between services, instead of hunting for each service's own
   `/swagger` URL. *Built* - `Features/Services/` plus the `/swagger` page.
2. **LaunchDarkly feature-toggle browser.** List the current toggles and their values, and copy
   a selection as JSON shaped for pasting into a local `appsettings.Development.json`. The
   copy-out format is the point of the feature, not a nicety. *Listing is built* -
   `Features/FeatureFlags/` plus the `/feature-flags` page; the copy-out is not.
3. **Service data browsing.** Lists of records, and a single-record view for a chosen row. *Built* -
   `Features/ServiceData/` plus the `/service-data` pages, over seven Personnel resources (Person
   and Employee services) and eleven DataIntegration resources (the Configuration API's clients,
   pipelines, connections, connectors, client resources, pipeline resources, pipeline executions
   and execution steps, plus connection credentials, connection certificates and an execution
   step's dataset - the last three reachable only via a relation button, never listed). One
   backend slice serves two frontend nav entries, "Personnel Browser" and "DataIntegration
   Browser" - see `ResourceDescriptor.Section`.
4. **Service status dashboard.** Whether every service under the `Services` configuration section
   answers its ping endpoint, refreshed while the page is open. *Built* - `Features/ServiceHealth/`
   plus the `/service-health` page.

Features 1, 3 and 4 are built, 2 in part. `Features/SampleItems/` is scaffolding that
proves the pipeline end to end; it is not one of them.

## Design decisions

- **The frontend talks only to this backend, and this backend is not a reverse proxy.** Avoid
  calling another Numo service from the browser: a developer machine cannot necessarily reach those
  services, while DevServices is deployed alongside them inside the network. But the way to do that
  is a narrow endpoint per need, each reaching a fixed path on a service named by a configuration
  key - never a pass-through route that forwards an arbitrary path, which would become a hole into
  the internal network if this app were ever exposed outside it.
- **Consequence for Swagger UI: "Try it out" reaches the service but the browser blocks the
  response.** Numo specs declare relative servers (`/employee-api`, `/`), which resolve against this
  app's own origin, so the served document gets the service's configured location prepended as the
  first server. Requests then go to the service directly - and the services send no
  `Access-Control-Allow-Origin` and answer `OPTIONS` with 405, so CORS stops them. Two ways round it
  locally: a CORS-disabling browser extension, which is what we do today and is accepted for a
  developer-only tool, or proxying the service prefix in `proxy.conf.js` so everything is
  same-origin - dev-server-only either way, adding nothing to the deployed app. Making Execute work
  in the deployed app would need the pass-through route above, which is why it does not work there.
- **Prefer a service's own client library.** If a service ships one - `Numo.Employee.Client.Lib`
  and its siblings - consume it instead of hand-rolling an HTTP client against its API.
- **Keep both ends lean, and drive the UI from data.** The frontend should render generic
  components against the JSON structure the backend returns, rather than growing a bespoke typed
  page per service and per record type. Expect exceptions where a feature genuinely needs its own
  shape - the toggle copy-out and the Swagger host both do - but a new list of records should
  ideally be a new backend endpoint and no new frontend code.

## Standing constraints

- **The LaunchDarkly SDK cannot list toggles, and this is why the toggle page uses the REST API.**
  `IFeatureFlagService` from `Numo.Common.Lib` only answers `IsEnabledAsync(name)` for a flag you
  already know, and the server SDK underneath it only evaluates a named flag against a context -
  `AllFlagsState` yields flag keys and values, never names, descriptions or tags, because those are
  project metadata that LaunchDarkly does not send to SDKs at all. So the flag list comes from
  `GET /api/v2/flags/{projectKey}`, which needs an API access token rather than the SDK key.
  Note also that `AddNumoCommonServices` does not register `ILdClient`: it builds its own inside a
  private factory, and resolving `IFeatureFlagService` with `Provider: LaunchDarkly` additionally
  needs an `IDistributedCache` that nothing here registers.
- **Every Numo service answers `api/platform/microservice/ping` with `pong`.** The path is the same
  under every service's configured location, so the status dashboard needs no per-service knowledge
  beyond that location. Up means HTTP 200 *and* a `pong` body: a gateway that is up while the
  service behind it is not still answers 200, with a page of its own, so the status code alone would
  read as healthy. One service being down is data rather than a failure, so the endpoint answers 200
  with a row per service either way.
- **Every Person and Employee call needs a `Numo-Tenant-Id` header, and nothing else.** Both
  services answer anonymously given that header and 401 without it, so the browsing feature carries
  no bearer token. The tenant id comes from a text field in the UI and is sent on every request. Two
  consequences: a well-formed tenant the services do not recognise answers an empty list rather than
  an error, so an empty grid must never be presented as "no data"; and nothing is requested at all
  until an id is entered. The header reaches the services through the client libraries, which read
  it from `INumoCurrentTenantService` - set per request through the supported `AddNumoTenantSetter`
  seam, not a custom implementation.
- **`FeatureManagement:AllowUnauthorizedApiCalls` exists only because this app has no authenticated
  principal.** Without it the Employee client libraries throw `UnauthorizedException` client-side,
  before a socket is opened, because their header handlers resolve `ICurrentPrincipalService`. It
  lives in the committed `appsettings.json` rather than the gitignored development file, or the
  feature works on one machine and throws on every other. Remove it when authentication lands.
- **Neither service reports a total count anywhere**, in no envelope and no header. So paging is
  next/prev only, `hasMore` is derived by fetching page N+1 and testing it for emptiness, and the UI
  shows no page count. Inflating `PageSize` to detect a next page is wrong, not merely inelegant:
  the server window is a function of `PageSize`, so asking for one extra row shifts the next page's
  start and silently skips a record at every boundary.
- **Ordering is opt-in per column and its syntax is narrow.** `OrderBy=name` ascends,
  `OrderBy=-name` descends, and `name desc`, `name:desc` and a comma-joined pair are all silently
  ignored, as is any column the service does not mark orderable. An empty `OrderBy=` makes the
  request fail outright, so the parameter is omitted rather than sent blank. Because an unsupported
  column fails silently, a column is only marked sortable after a probe showed it actually sorts.
- **No human name exists anywhere in the Employee service.** `EmployeeDto` is ids, a code, an
  email and a phone; names live only on `PersonDto`. So the employees and positions grids resolve
  names with one batched `IPersonClient` call per page, capped because the client library switches
  to a `POST {endpoint}/search` above 1000 characters of query string and
  `/api/positions/view/search` does not exist at all.
- **There is no LegalRelation entity anywhere in `Numo.Employee.Lib`.** `PositionDto` and
  `AbsenceDto` each carry a bare `LegalRelationId`, and `PositionFilter`/`AbsenceFilter` can filter
  by it, but no client or route ever returns a legal relation's own data - not even a name. So it
  is not a resource of its own; a position's or absence's "Legal relation id" field carries two
  relations instead ("Positions" and "Absences" filtered by that id), the same shared-foreign-key
  pattern as everything else here, rather than a link to a record that does not exist or an
  invented standalone list.
- **No DataIntegration resource uses a client library.** `Numo.DataIntegration.Configuration.Lib`'s
  real `IConfigurationClient` (25 methods, dumped by reflection, not read from documentation) has no
  list method for clients, pipelines, connectors, connections, client resources, pipeline resources,
  pipeline executions or execution steps, and no by-id method for the nested certificates or
  credentials routes - it is built for point lookups by name, not for browsing. So every
  DataIntegration resource reads the Configuration API directly over one shared hand-rolled
  `HttpClient` (`Resources/DataIntegrationConfigurationApi.cs`); `Numo.DataIntegration.Connectors.Lib`
  is not referenced at all, since none of the chosen resources need the Connectors API. Neither the
  service nor its client needs a tenant header - both were verified anonymous.
- **Neither DataIntegration route pages**, so its resources fetch the whole (small, configuration-
  sized) list once and slice it in memory, unlike the fetch-per-page rule above.
- **A resource that cannot stand alone never appears in the picker tabs**, only under its parent
  record: `IsReachableOnlyByRelation` resources and any resource with a required filter (a parent
  id supplied only by a relation) are both excluded from `pickerResources` in
  `service-data-page.ts`, opened directly they would show nothing. Every relation on a record
  renders inline via `app-related-records-panel` instead of navigating to a separate page - an
  ordinary nested resource (`di-client-resources` and its siblings) loads automatically, while an
  `IsReachableOnlyByRelation` target stays behind a button that expands the list in place, so it is
  still fetched only on demand. Each panel still links out to the full list page (still reachable by
  URL, just not tabbed) for paging, sorting or filtering beyond the ten-row preview.
- **Credentials, certificates and dataset records are shown, but never in a list.**
  `di-connection-credentials`, `di-connection-certificates` and `di-execution-step-dataset` are
  reachable only via a relation on their parent record
  (`ResourceDescriptor.IsReachableOnlyByRelation`), fetched only when their panel's button is
  pressed. `di-connections` itself never sends the `expand` query parameter that would embed credentials or
  certificates into a list - its own DTO has no property for either, so even an unexpected
  expansion could not leak into a cell. A dataset's own fields have no fixed schema (they are
  whatever the source connector produced), so its detail record renders every field the record
  carries rather than declaring columns in advance. `connector-key` and `numo-key` are confirmed
  non-secret identifiers and would be shown as ordinary fields if a later pass adds the extra
  fan-out call they need.
- **A dataset's own paging is continuation-token based, not page-index**, unlike every other route
  in this slice. `di-execution-step-dataset` fetches one batch and stops rather than pretending to
  support Previous/Next it cannot honour; `ResourcePage.Notice` says so when the service's own token
  shows more records exist.
- **`RelationDescriptor` carries a `Filters` dictionary**, the same shape `ResourceQuery.Filters`
  already uses - one entry for nearly every relation, two for `di-execution-step-dataset`
  (`executionId` and `stepId`, the one target that needs two parent ids at once).
  `RelationDescriptor.To(label, target, key, value)` is a convenience constructor for the common
  one-filter case, not a second field or a second way to represent a relation.
- **`department-roles` is the one Personnel resource with no client library.** `DepartmentRoleDto` is
  internal in `Numo.Employee.Lib` and no client method exposes the unfiltered route, so that
  resource owns an `HttpClient`, a local record, the response envelope and its own tenant header. It
  is the exception, commented as such in the file, and parsing the envelope itself buys it the one
  thing the libraries cannot give: an absent record recognised from the error's structured metadata
  rather than from the wording of its message.
- **Listing flags takes two calls, because per-environment state is opt-in.**
  `GET /api/v2/flags/{projectKey}` omits the `environments` object entirely unless every wanted
  environment is named in a repeated `env` parameter, so the project's environments are read from
  `GET /api/v2/projects/{projectKey}/environments` first. In the default summary representation an
  environment then carries `on` and a `_summary` marking which variation index is the fallthrough
  and which is the off one - that is where the served value comes from. The plain `fallthrough` and
  `offVariation` fields appear only under `summary=0`, which also returns every targeting rule, so
  the summary is both the smaller and the sufficient answer.

## Ideas for later

- **Replace Swagger UI with our own request UI.** No embeddable OpenAPI viewer persists what you
  type: swagger-ui, Scalar, RapiDoc and Stoplight Elements each store authorization and nothing
  else, and none exposes an API to write values back into its fields, so saved requests are
  impossible from the outside. Rendering the request form ourselves from the spec would let a
  request be saved, named and replayed - in this app's database, not just the browser - and it is
  the same generic spec-driven renderer the data-browsing feature needs. Sizeable, and only worth it
  once retyping requests actually hurts; Insomnia covers that gap today by importing the spec from
  `api/services/{name}/openapi`.

## Open questions

- **When does the service registry move into the database?** The list currently comes from the
  `Services` section of `appsettings.Development.json` via `IServiceDiscoveryService`. The intended
  next step is storing it in this app's database and editing it through the UI, which is what the
  scaffolded EF Core setup is for. The swap is a second implementation of that same interface plus
  a registration change - handlers do not know where the list came from.
- **Answered: "generic components from JSON" is an explicit display descriptor.**
  `Features/ServiceData/` settles it. Each resource declares its columns, filters and relations in
  C#, and the frontend has two components that render whatever arrives. Seven resources needed no
  per-resource frontend code, and a cell or field carries an optional link so grid-to-grid
  navigation is data rather than TypeScript. The column-inferring alternative was rejected because
  the frontend would have to guess labels and formats and could not know the relations at all.
