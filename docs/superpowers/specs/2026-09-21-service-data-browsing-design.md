# Service data browsing - design

Drafted 2026-09-21. Answers `docs/Purpose.md` planned feature 3 and its open question about what
"generic components from JSON" means concretely.

Every platform fact below was verified by probe against `test.numo.lv` and against the client library
assemblies, not read from documentation. A first version of this design was invalidated by that
verification; the findings that changed it are recorded under "Corrections to the first draft" so the
next reader does not repeat them.

## Goal

Browse Person and Employee service data from this tool: lists with pagination, search where the
services offer it, a full-data view for any record, and navigation between related records. As generic
as feasible - the target property is that a new resource costs one backend class and no frontend code.

Read-only throughout. The slice issues GET requests only and exposes no create, update or delete path.

## Decisions taken before design

- Implement both services now. Authentication is out of scope for this slice.
- The tenant id comes from a text field in the UI, persisted in `localStorage`.
- The backend reaches the services through `Numo.Person.Lib` and `Numo.Employee.Lib` where possible.
- `department-roles` is included as a documented exception to the client-library rule (see below).
- Test tenant `9efa8e6b-0f37-4bc7-9d1f-184040d4a2de` is used read-only. No writes to either service.

## Verified platform behaviour

These are the facts the design rests on. Each was observed, not assumed.

- **Both services answer anonymously given a tenant header.** `GET /employee-api/api/employees` returns
  401 with no header and 200 with `Numo-Tenant-Id` alone. There is no bearer token anywhere in this
  design; an earlier reading of that 401 as missing authentication was wrong.
- **The client libraries throw before opening a socket.** `AddEmployeeClient` installs
  `NumoAccountIdHeaderHandler`, which resolves `ICurrentPrincipalService` and throws
  `Numo.Common.Lib.Exceptions.Authorization.UnauthorizedException` ("Cannot use ICurrentPrincipalService
  when processing requests without authorization info") in an app with no authenticated principal.
  Setting `FeatureManagement:AllowUnauthorizedApiCalls` to `true` permits the header handlers to inject
  tenant, account and person ids, after which the calls succeed. This is configuration, not code.
- **The supported tenant seam is `AddNumoTenantSetter`.** `Numo.Common.Microservice.Lib.CurrentTenant`
  exposes `AddNumoTenants()` and `AddNumoTenantSetter(ServiceLifetime)`, plus
  `Setter.INumoTenantSetterService.SetCurrentTenantId(Guid)`. The internal implementation holds the
  override in an `AsyncLocal`, so a value set at the start of a request scope reaches the client
  libraries' handlers. `Add*Client()` calls `AddNumoTenants()` itself.
- **Paging is page-index, and the window is a function of `PageSize`.** `Page` is 1-based and `Page=0`
  clamps to page 1. `PageSize` is honoured exactly; no server cap appeared up to 500. Consequently
  asking for `pageSize + 1` rows to detect a next page is arithmetically wrong: page 1 would cover rows
  1-21 and page 2 rows 22-42, so row 21 is never displayed.
- **No total count exists** in the `{isSuccessful, errors, value}` envelope or in any response header,
  on any resource of either service.
- **Ordering is opt-in per column and its syntax is narrow.** `OrderBy=name` sorts ascending,
  `OrderBy=-name` descending. `name desc`, `name:desc`, `name.desc` and a comma-joined pair are all
  silently ignored, as is any column the service does not mark orderable (`id` on job titles, for one).
  An empty `OrderBy=` makes the request fail with `isSuccessful: false`, so the parameter must be
  omitted rather than sent blank.
- **Enums cross the wire as integers** (`absence.type: 0`, `position.schedule: 1`). The library DTOs
  type these properties as enums, so projecting through the libraries yields names for free. This is
  the strongest single argument for the libraries over hand-rolled JSON.
- **No human name exists in the Employee service.** `EmployeeDto` is `id, deletedAt, personId, code,
  email, phone`. Names live only on `PersonDto`. Employees and positions are illegible without a
  cross-service lookup.
- **`PositionFilter` has no `Ids` and there is no `/api/positions/view/{id}`.** The embedded
  employee/department/job-title objects the list view enjoys cannot be had for a single position.
- **`BaseClient` switches to `POST {endpoint}/search` above 1000 characters of query string**, and
  `/api/positions/view/search` does not exist. A repeated `PersonIds=<guid>` costs about 46 characters,
  so roughly 22 ids is the ceiling before that fallback 404s.
- **`DepartmentRoleDto` is `internal`** and `IDepartmentClient` exposes no roles method; the route is
  used internally, pre-filtered to `RoleType.Manager`.

## Architecture: backend

One new slice, `Features/ServiceData/`. Packages added: `Numo.Person.Lib` 3.0.0.242 and
`Numo.Employee.Lib` 5.0.0.241 and nothing else. An explicit `Numo.Common.Microservice.Lib` 5.0.0.247
reference breaks restore with NU1605 (it requires `Numo.Common.Lib >= 4.0.0.205` against this project's
pinned 4.0.0.201); the tenant interfaces arrive transitively at 4.0.0.222 and compile.

### The resource abstraction

```csharp
public interface IServiceDataResource
{
    ResourceDescriptor Descriptor { get; }
    Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken);
    Task<ResourceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
```

Public, not internal: a public handler cannot take an internal constructor parameter (CS0051), and the
`FeatureFlags` slice already sets that precedent with `LaunchDarklyApiOptions`.

A resource implementation may issue **several** downstream calls per method. This is not a concession,
it is a requirement: employees and positions need a batched person lookup to show a name at all, and a
position's detail needs four extra fetches. Any abstraction assuming one call per method is wrong for
this data.

`ServiceDataCatalogue` resolves `IEnumerable<IServiceDataResource>` and indexes it by descriptor key.

### Wire shapes

Returned to the frontend verbatim, and the frontend renders nothing that is not described here.

```csharp
public sealed record ResourceDescriptor(
    string Key, string Label, string ServiceName,
    IReadOnlyList<ColumnDescriptor> Columns,
    IReadOnlyList<FilterDescriptor> Filters);

public sealed record ColumnDescriptor(string Key, string Label, FieldKind Kind, bool IsSortable);
public sealed record FilterDescriptor(string Key, string Label, FilterKind Kind,
    IReadOnlyList<string>? Options);

public sealed record ResourcePage(
    IReadOnlyList<ResourceRow> Rows, int Page, int PageSize, bool HasMore, string? Notice);

public sealed record ResourceRow(Guid Id, DateTimeOffset? DeletedAt, IReadOnlyList<Cell> Cells);
public sealed record Cell(string? Value, RecordLink? Link);

public sealed record ResourceRecord(
    Guid Id, string Title,
    IReadOnlyList<FieldValue> Fields,
    IReadOnlyList<RelationDescriptor> Relations);

public sealed record FieldValue(string Label, string? Value, FieldKind Kind, RecordLink? Link);
public sealed record RecordLink(string TargetResource, Guid Id);
public sealed record RelationDescriptor(string Label, string TargetResource, string FilterKey,
    string FilterValue);

public sealed record ResourceQuery(
    int Page, int PageSize, string? SortColumn, bool IsSortDescending,
    IReadOnlyDictionary<string, string> Filters);

public enum FieldKind { Text, Guid, Date, DateTime, Number, Boolean, Enum }
public enum FilterKind { Text, Guid, Date, Boolean, Enum, GuidList }
```

`FilterKind.GuidList` was added during implementation: the Employee service's id filters are arrays,
and a reverse relation onto one of them cannot be expressed by a single id. It carries several ids in
one value, comma delimited, capped at four by the page validator so an overlong list is a stated 400
rather than a downstream request the client library turns into a POST.

**The wire form of a filter** is `filters[key]=value`, keyed by `FilterDescriptor.Key`. The controller
reads those by hand rather than by model binding, because given no `filters[...]` key at all the
dictionary binder falls back to the bare query string and swallows `page` and `pageSize` as filters. A
bare key is therefore ignored rather than rejected, which is why the frontend's API service translates
into the bracket form. The frontend's own route URL is a separate thing and keeps bare keys.

`ResourceQuery.Filters` is a dictionary keyed by `FilterDescriptor.Key`, so a resource reads only the
filters it declared and an unknown key is rejected by the action's validator rather than passed
downstream. The two `Kind` enums are closed sets: a resource needing a kind that is not listed is the
one case that costs a frontend change, and adding one is a deliberate edit to both ends.

`Cell` carries an optional link because grid-to-grid navigation is a stated goal and a plain string
cannot express it. `ResourceRow.DeletedAt` is uniform: every round-one DTO carries `deletedAt`.
`ResourcePage.Notice` is how a resource reports that it truncated something, which the person-name
search needs.

### Paging

`PageSize` is sent unchanged, and `HasMore` comes from fetching page N+1 and testing it for emptiness.
That is one extra call per page view, and it is the only correct answer on a page-index API with no
total. `OrderBy` is sent only when a sort is requested, never empty, in the `field` / `-field` form, and
only for columns whose `ColumnDescriptor.IsSortable` is true - a per-resource fact established by probe
during implementation, since neither the spec nor the client library exposes it.

### Tenant

The frontend sends the platform's own header name, `Numo-Tenant-Id`. `ServiceDataRegistration` calls
`AddNumoTenantSetter(ServiceLifetime.Scoped)`; a slice-scoped action filter parses the header and calls
`INumoTenantSetterService.SetCurrentTenantId(...)` before any handler runs. A missing or unparseable id
fails fast as `TenantIdMissing` with no downstream call. No custom `INumoCurrentTenantService` and no
registration-order trick: the first draft reinvented a seam the platform already provides.

`FeatureManagement:AllowUnauthorizedApiCalls` is set to `true` in `appsettings.json` with a comment
recording that it exists only because this tool has no authenticated principal, and that it must be
removed when authentication lands. It belongs in the committed file, not the gitignored development
one, or the feature works on one machine and throws on every other. Expect one warning per downstream
call from the platform; turn that log category down if it becomes noise.

### Actions and errors

One file per action, per the repo convention: `GetServiceDataResources.cs` (`GET /api/service-data`),
`GetServiceDataPage.cs` (`GET /api/service-data/{resource}`), `GetServiceDataRecord.cs`
(`GET /api/service-data/{resource}/{id}`). Each with its request record, `AbstractValidator`, result
record and handler returning `NumoResult<T>`. The controller only dispatches.
`ServiceDataRegistration.cs` holds all six `Add*Client()` calls - `AddPersonClient` plus
`AddEmployeeClient`, `AddPositionClient`, `AddJobTitleClient`, `AddDepartmentClient` and
`AddAbsenceClient`, because `AddEmployeeClient()` registers only the employee client and not the other
four from that library - plus the tenant setter, the filter, and the department-roles HTTP client.

`ServiceDataErrors.cs`: `UnknownResource`, `TenantIdMissing`, `RecordNotFound`, `DownstreamCallFailed`,
`DownstreamCallUnauthorized`. The last is separate because the client libraries throw
`UnauthorizedException` client-side, where there is no status code to report; `DownstreamCallFailed`
carries an optional status code for genuine transport and HTTP failures.

`{resource}` is a key into a closed, compile-time set of registered resources, each bound to a fixed
path on a service named by a configuration key. It is not a pass-through route and forwards no
caller-supplied path, so `docs/Purpose.md`'s standing "not a reverse proxy" decision holds.

## Resource catalogue

| key | client | list route | detail | notes |
|---|---|---|---|---|
| `persons` | `IPersonClient` | `/api/persons` | `GetPerson(id)` | text search on name and email |
| `employees` | `IEmployeeClient` + `IPersonClient` | `/api/employees` | `GetEmployee(id)` | person lookup per page for the name |
| `positions` | `IPositionClient` + `IPersonClient` | `/api/positions/view` | fan-out, see below | list shows embedded names |
| `job-titles` | `IJobTitleClient` | `/api/job-titles` | `GetJobTitle(id)` | |
| `departments` | `IDepartmentClient` | `/api/departments` | `GetDepartment(id)` | children via `GetDepartmentHierarchy` |
| `absences` | `IAbsenceClient` | `/api/absences` | `GetAbsence(id)` | |
| `department-roles` | hand-rolled | `/api/departments/roles` | `/api/departments/roles/{id}` | documented exception |

**`department-roles` is a deliberate exception** to "reach services through their client libraries". Its
DTO is `internal` and no client method exposes the unfiltered route, so this resource owns a typed
`HttpClient` pointed at `Numo.Employee.Api`'s location from `IServiceDiscoveryService`, a local
`DepartmentRole` record, and its own `Numo-Tenant-Id` header - that header normally comes from the
library's handler pipeline, which this client does not have. This is the one resource where the
"one class per resource" property costs extra, and it is written down so nobody mistakes it for the
pattern.

**Names.** After fetching a page of employees or positions, the resource collects the distinct
`personId`s and issues one `IPersonClient.GetPersons(new PersonFilter { PersonIds = ids })`, projecting
the name into the row. Without it these two grids are columns of GUIDs.

**Position detail** fans out: `GetPosition(id)` plus employee, department and job title by id, plus the
person for the name. Five calls for one page, accepted deliberately for a developer tool, because
neither a by-id view route nor an `Ids` filter on positions exists.

**Search.** `persons` gets `fullNamePart` and `email` directly. `employees` and `positions` get a
person-name filter implemented as a pre-search: `persons?FullNamePart=x` yields ids which feed
`PersonIds`, and the cap sets `ResourcePage.Notice` so the UI says it is showing the first N matching
people rather than silently lying.

**Corrected after measurement:** this paragraph said "capped at 20 ids" for both. 20 is unaffordable.
The caps were measured by calling each filter's own `ToQueryString()` against the 1000-character
ceiling, and they differ per resource because each filter carries a different parameter set:
`employees` caps at **18** (911 characters worst case), `positions` at **8** (891 characters alongside
a four-value list filter). Above the ceiling the client library switches to `POST {endpoint}/search`,
which breaks the GET-only constraint on `employees` and 404s outright on `positions`, where no such
route exists.

Structured filters are declared where they exist: absences `From`/`Till`/`Status` (singular, because
the wire contract has no list-valued enum kind and a plural key would promise a list it cannot carry),
positions `Primary`/`From`/`Till`/`DepartmentIds`, departments `ActiveFrom`/`ActiveTo`, department
roles `Active`/`RoleType`, persons `IsActive`. Both `employees` and `absences` also declare
`employeeIds`, and `absences` and `positions` declare `departmentIds`, because other resources'
reverse relations point at those keys.

**Relations.** Forward links come from the foreign keys on each DTO. Reverse links are only declared
where a matching downstream filter exists: person to employees, employee to positions and absences and
roles, department to positions and roles, job title to nothing. Two relations the first draft claimed
are dropped because no filter can express them: positions by job title, and departments by parent
(children come from `GetDepartmentHierarchy` instead). Position to person is a genuine two-hop through
`personId`, and the detail page resolves it so the link is direct.

**Soft deletes** are excluded by default. `IncludeDeletedSince` is a date, not a flag, so "include
deleted" means passing a far-past date; when set, `ResourceRow.DeletedAt` renders as a badge.

## Frontend

`features/service-data/` with `api/`, two generic components, and two pages.

- `components/generic-record-table` renders `ResourceRow.Cells` against `ColumnDescriptor`s, a cell with
  a link becoming a `routerLink` to `/service-data/{targetResource}/{id}`. One template branch, no
  per-resource code.
- `components/generic-record-detail` renders `FieldValue`s and a button per `RelationDescriptor`.
- `components/tenant-id-field` owns the tenant id: a signal persisted to `localStorage`. With no tenant
  set the pages render a prompt and fire no requests.
- `pages/service-data-page` at `/service-data/:resource` - resource picker, filter bar built from the
  descriptor, table, and **plain Previous/Next buttons**. PrimeNG 21.1.9's paginator derives its state
  from `ceil(totalRecords / rows)`, so with no total both arrows are permanently disabled; two buttons
  are less code and claim nothing the data cannot support.
- `pages/service-data-record-page` at `/service-data/:resource/:id`.

Resource, filters and page live in the query string, so relation links are ordinary router links and the
back button works. The page reacts to route parameter changes rather than loading in the constructor,
because Angular reuses the component instance across `:resource` changes.

The tenant header is attached in this feature's own API service. A "feature-scoped interceptor" does not
exist in this app's wiring, and a global interceptor would leak the header onto the LaunchDarkly and
ping calls.

Nav item "Service data", after "Service status".

## Security decision, recorded deliberately

This slice makes an unauthenticated, developer-only tool a reader of any tenant's personal data -
`firstName`, `lastName`, `email`, `phone`, `personCode` - for whatever tenant id a caller types in. No
credential is involved: the downstream services enforce nothing beyond the header's presence.

This is accepted on the grounds that the app is network-internal and developer-only, and that its data
is test data. It is recorded because it changes the tool's exposure class, and because
`docs/Purpose.md`'s "not a reverse proxy" decision was written to prevent exactly this risk arriving by
the path route - it arrives here by the data route instead. `Features/ServiceData/` is therefore the
first slice that must gain `[Authorize]` the moment authentication lands, and that dependency is named
in `docs/Architecture.md` rather than left implicit.

## Verification plan

No test project exists, so verification is a build plus an explicit probe list against tenant
`9efa8e6b-0f37-4bc7-9d1f-184040d4a2de`, read-only.

1. `dotnet build` clean, `npm run build` clean.
2. Every resource: page 1 and page 2 at `pageSize=5`, asserting no row id appears in both. This is the
   probe that would have caught the `pageSize + 1` defect.
3. Every resource: `HasMore` true on a full page and false on the last one.
4. Every sortable column: ascending and descending change the first row, and a sort is never requested
   for a column the descriptor does not mark sortable.
5. Every resource: one detail record renders, and a random unknown GUID yields `RecordNotFound`, not a
   500 or an empty page.
6. Every declared relation followed once, in both directions where declared.
7. Person-name search on `employees` and `positions`, including a term matching more than 20 people, to
   confirm the notice appears and no 404 occurs.
8. Enum columns show names, not integers.
9. No tenant, a malformed tenant, and a well-formed but unknown tenant each produce a distinguishable
   message rather than a bare empty grid.

## Corrections to the first draft

Kept so the reasoning is auditable: the bearer-token story (the 401 was a missing tenant header);
`pageSize + 1` for `HasMore` (drops one row per page boundary); an `internal` resource interface (does
not compile); a custom `INumoCurrentTenantService` with a registration-order trick (a supported seam
exists); an explicit `Numo.Common.Microservice.Lib` reference (breaks restore); `X-Numo-Tenant-Id` (the
platform's name is `Numo-Tenant-Id`); cells as plain strings (cannot carry links); `department-roles`
via a client library (impossible); positions by job title and departments by parent as reverse relations
(no filters); "search is persons only" (employees and positions can search by person name); and the
PrimeNG paginator (needs a total it cannot have).

## Out of scope

Writes of any kind. Authentication. Entity-level tenancy in this app's own database, which stays absent.
Resources beyond the seven above. The `positions/view` embedded objects on a detail page.
