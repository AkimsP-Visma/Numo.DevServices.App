# Service Data Browsing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended)
> or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`)
> syntax for tracking.

**Goal:** Browse Person and Employee service data in this tool - paginated lists, search where the
services offer it, a full-data view per record, and navigation between related records - driven by a
per-resource descriptor so the frontend stays generic.

**Architecture:** One backend slice, `Features/ServiceData/`, where each resource is a class
implementing `IServiceDataResource` that wraps its typed client and declares its own columns, filters
and relations. Three mediator actions expose a catalogue, a page and a record. The Angular feature has
two generic components that render whatever the descriptor says, so a new resource is one backend class
and no frontend change.

**Tech Stack:** .NET 10, numo-core 7.0.248, `Numo.Person.Lib` 3.0.0.242, `Numo.Employee.Lib` 5.0.0.241,
EF Core 10 (untouched here), Angular 21 standalone/signals/zoneless, PrimeNG 21.1.9.

**Spec:** `docs/superpowers/specs/2026-09-21-service-data-browsing-design.md` - read it before starting.
It records why each decision is what it is, and the verified platform behaviour that contradicts the
obvious approach in several places.

## Global Constraints

Every task's requirements implicitly include all of this.

- **Read-only.** GET requests only. Never register `AddPersonTestClient` / `Add*TestClient`: those
  clients expose `Insert`, `Update` and `Delete`. No write path reaches either service, ever.
- **Test tenant `9efa8e6b-0f37-4bc7-9d1f-184040d4a2de` is read-only.** All probing uses it. No
  modifications to Person or Employee data.
- **Never print personal data.** Probe output must be reduced to counts, ids or field names before it
  reaches the terminal, a log, or a commit message. Never log a row body at Information level.
- **Register the module exactly once.** `AddNumoWebApi<DevServicesModule>()` already calls
  `AddNumo<DevServicesModule>()`. Prefer assignment over accumulation in any `ConfigureHttpClient`
  action, per `CLAUDE.md`.
- **`OrderBy` is never sent empty.** An empty `OrderBy=` makes the downstream request fail with
  `isSuccessful: false`. Omit the parameter instead. Ascending is `field`, descending is `-field`; no
  other syntax works.
- **`PageSize` is sent unchanged.** Never inflate it to detect a next page - the server window is a
  function of `PageSize`, so that silently skips a row at every page boundary.
- **No client method takes a `CancellationToken`.** Handlers still accept one per repo convention and
  honour it around the calls they control; it cannot be passed downstream. Do not invent an overload.
- **Writing style:** no em dashes or en dashes anywhere, including code comments and commit messages.
  Use a hyphen. Comments explain why, not what.
- **Files are CRLF.** Match the working tree; do not introduce LF lines into existing files.
- **Docs wrap at 100 columns.**
- **No test project exists and none is added.** Verification per task is `dotnet build`, `npm run build`
  where the frontend changed, and the named curl probes. This is a deliberate repo convention, not an
  oversight - see "Deliberately absent" in `CLAUDE.md`.

## Platform facts that will bite

Each was verified by probe. Treat them as given; re-probing is cheap if you doubt one.

- Both services answer anonymously **given a tenant header**. There is no bearer token in this feature.
- The Employee client libraries throw `UnauthorizedException` *client-side, before any socket opens*,
  unless `FeatureManagement:AllowUnauthorizedApiCalls` is `true`.
- `IPersonClient` returns bare `Task<PersonDto>` / `Task<IEnumerable<PersonDto>>` and signals failure by
  throwing. Every Employee-library client returns `Task<Result<T>>` and signals failure through the
  result. The two error paths are different and both must be handled.
- `Page` is 1-based; `Page=0` clamps to page 1. No total count exists anywhere.
- Enums arrive as integers over HTTP, but the library DTOs type them as enums, so projecting through a
  client yields names.
- `EmployeeDto` has no name field. Names live only on `PersonDto`.
- There is no `/api/positions/view/{id}` and `PositionFilter` has no `Ids`.
- `DepartmentRoleDto` is `internal` and no client method exposes the unfiltered roles route.

## File Structure

**Backend - `src/Numo.DevServices.Api/Features/ServiceData/`**

| file | responsibility |
|---|---|
| `ServiceDataContracts.cs` | the wire shapes from the spec and the two `Kind` enums, nothing else |
| `IServiceDataResource.cs` | the resource abstraction |
| `ServiceDataCatalogue.cs` | indexes the registered resources by key; the only lookup point |
| `ServiceDataErrors.cs` | the five `NumoError`s with stable ids |
| `ServiceDataRegistration.cs` | the six `Add*Client` calls, the tenant setter, the filter, the roles client, the seven resource registrations |
| `TenantIdActionFilter.cs` | reads the request header and pushes it into the tenant setter |
| `GetServiceDataResources.cs` | catalogue action: request, validator, result, handler |
| `GetServiceDataPage.cs` | page action |
| `GetServiceDataRecord.cs` | record action |
| `ServiceDataController.cs` | dispatch only |
| `Resources/PersonsResource.cs` | one file per resource, seven in total |
| `Resources/EmployeesResource.cs` | list plus the batched person lookup |
| `Resources/PositionsResource.cs` | view list plus the five-call detail fan-out |
| `Resources/JobTitlesResource.cs` | |
| `Resources/DepartmentsResource.cs` | list plus hierarchy children |
| `Resources/AbsencesResource.cs` | |
| `Resources/DepartmentRolesResource.cs` | the hand-rolled exception |
| `Resources/PersonNameLookup.cs` | shared by employees and positions: id-to-name batching and the name pre-search, so neither resource owns logic the other needs |

`Resources/` is a subfolder because seven resource files plus ten slice files in one flat folder is
harder to read than the split, and everything still lives inside the slice.

**Frontend - `ClientApp/src/app/features/service-data/`**

| file | responsibility |
|---|---|
| `api/service-data.model.ts` | TypeScript mirrors of the wire shapes |
| `api/service-data-api.service.ts` | the three calls, and the only place the tenant header is attached |
| `state/tenant-id.store.ts` | the tenant id signal and its `localStorage` persistence |
| `components/tenant-id-field/` | the input plus a "no tenant set" prompt |
| `components/generic-record-table/` | descriptor-driven table, sortable headers, cell links |
| `components/generic-record-detail/` | fields and relation buttons |
| `components/resource-filter-bar/` | inputs built from `FilterDescriptor`s; separate because filter widget types will grow |
| `pages/service-data-page/` | list page: picker, filter bar, table, Previous/Next |
| `pages/service-data-record-page/` | detail page |
| `service-data.routes.ts` | the two lazy routes |

**Modified:** `Numo.DevServices.Api.csproj`, `DevServicesModule.cs`, `appsettings.json`,
`appsettings.Development.Template.json`, `ClientApp/src/app/app.routes.ts`,
`ClientApp/src/app/core/layout/app-layout.ts`, `CLAUDE.md`, `docs/Purpose.md`, `docs/Architecture.md`.

---

### Task 1: Packages, the unauthorized-calls flag, and the tenant seam

Nothing user-visible. The deliverable is that the client libraries can be resolved and will send a
tenant id, proven by a throwaway probe endpoint that is removed before the commit.

**Files:**
- Modify: `src/Numo.DevServices.Api/Numo.DevServices.Api.csproj`
- Modify: `src/Numo.DevServices.Api/appsettings.json`
- Modify: `src/Numo.DevServices.Api/appsettings.Development.Template.json`
- Create: `src/Numo.DevServices.Api/Features/ServiceData/ServiceDataRegistration.cs`
- Create: `src/Numo.DevServices.Api/Features/ServiceData/TenantIdActionFilter.cs`
- Modify: `src/Numo.DevServices.Api/DevServicesModule.cs`

**Interfaces:**
- Consumes: `IServiceDiscoveryService` and `AddNumoCommonServices`, already registered by the module.
- Produces: `AddServiceDataFeature()` on `IServiceCollection`; `TenantIdActionFilter`, which later tasks
  attach to the controller; the tenant id constant for the header name.

- [ ] **Step 1:** Add `Numo.Person.Lib` 3.0.0.242 and `Numo.Employee.Lib` 5.0.0.241 to the csproj. Add
      nothing else. An explicit `Numo.Common.Microservice.Lib` reference is what NU1605 objects to; its
      types arrive transitively.
- [ ] **Step 2:** Run `dotnet build -t:Compile` and confirm restore succeeds with no NU1605 or NU1107.
      If a downgrade warning appears, stop and re-read the spec's package paragraph rather than bumping
      versions ad hoc.
- [ ] **Step 3:** Add `FeatureManagement:AllowUnauthorizedApiCalls: true` to `appsettings.json`, with a
      comment saying it exists only because this tool has no authenticated principal and must be removed
      when authentication lands. It goes in the committed file, not the gitignored development one.
      Mirror it into `appsettings.Development.Template.json`.
- [ ] **Step 4:** Write `ServiceDataRegistration.cs` with `AddServiceDataFeature()`: the six client
      registrations (`AddPersonClient`, `AddEmployeeClient`, `AddPositionClient`, `AddJobTitleClient`,
      `AddDepartmentClient`, `AddAbsenceClient` - the employee one registers only itself), and
      `AddNumoTenantSetter(ServiceLifetime.Scoped)`. No test-client registrations.
- [ ] **Step 5:** Write `TenantIdActionFilter.cs`: read the `Numo-Tenant-Id` request header, parse it as
      a `Guid`, and on success call `INumoTenantSetterService.SetCurrentTenantId`. On a missing or
      unparseable value, short-circuit with a problem-details response rather than letting a downstream
      call happen. Keep the header name in one constant.
- [ ] **Step 6:** Call `AddServiceDataFeature()` from `DevServicesModule.ConfigureServices`, after the
      existing feature calls. Do not add a second `AddNumo` call anywhere.
- [ ] **Step 7:** Add a temporary probe endpoint that resolves `IEmployeeClient` and `IPersonClient` and
      returns only the two row counts. This exists to prove the plumbing and is deleted in step 10.
- [ ] **Step 8:** Run the app on a spare port in Release with `--no-launch-profile` so the user's own
      instance keeps its binary. Probe the temporary endpoint with the tenant header and confirm both
      counts are non-zero. A thrown `UnauthorizedException` here means step 3 did not take effect.
- [ ] **Step 9:** Probe `/api/services` and `/api/service-health` on the same instance and confirm they
      still answer 200, so the new registrations broke nothing. Confirm the startup log shows the module
      configuring once.
- [ ] **Step 10:** Delete the temporary probe endpoint. Re-run `dotnet build -t:Compile` clean.
- [ ] **Step 11:** Commit: packages, the flag, and the tenant seam.

---

### Task 2: Contracts, catalogue, the three actions, and the `persons` resource

The first resource end to end, which is what makes the contract real. Do not write the other six here;
if the shapes are wrong, this is where it is cheap to find out.

**Files:**
- Create: `Features/ServiceData/ServiceDataContracts.cs`, `IServiceDataResource.cs`,
  `ServiceDataCatalogue.cs`, `ServiceDataErrors.cs`, `GetServiceDataResources.cs`,
  `GetServiceDataPage.cs`, `GetServiceDataRecord.cs`, `ServiceDataController.cs`,
  `Resources/PersonsResource.cs`
- Modify: `Features/ServiceData/ServiceDataRegistration.cs`

**Interfaces:**
- Consumes: `AddServiceDataFeature()` and `TenantIdActionFilter` from Task 1; `IPersonClient` and
  `PersonFilter` (`PageSize`, `Page`, `OrderBy`, `PersonIds`, `Email`, `FullNamePart`,
  `IncludeDeletedSince`, `IsActive`) from `Numo.Person.Lib`.
- Produces: every wire shape and both `Kind` enums; `IServiceDataResource` with its three members;
  `ServiceDataCatalogue.Find(key)`; the five error ids. Tasks 3-6 add resources against exactly this
  abstraction and must not change it - if one of them needs a change, that is a finding to raise, not a
  quiet edit.

- [ ] **Step 1:** Write `ServiceDataContracts.cs` with the shapes exactly as the spec lists them:
      `ResourceDescriptor`, `ColumnDescriptor`, `FilterDescriptor`, `ResourcePage`, `ResourceRow`,
      `Cell`, `ResourceRecord`, `FieldValue`, `RecordLink`, `RelationDescriptor`, `ResourceQuery`, and
      the `FieldKind` / `FilterKind` enums. All public - a public handler cannot take an internal
      parameter type.
- [ ] **Step 2:** Write `IServiceDataResource.cs` and `ServiceDataCatalogue.cs`. The catalogue takes
      `IEnumerable<IServiceDataResource>` and indexes by `Descriptor.Key`; it is the only place a key
      becomes a resource.
- [ ] **Step 3:** Write `ServiceDataErrors.cs` with `UnknownResource`, `TenantIdMissing`,
      `RecordNotFound`, `DownstreamCallFailed` and `DownstreamCallUnauthorized`, each with a stable id.
      `DownstreamCallFailed` carries an optional status code; `DownstreamCallUnauthorized` carries none,
      because the client-side throw has no status.
- [ ] **Step 4:** Write the three action files, each with its request record, its validator (rule-less
      where there is nothing to validate, since the mediator fails a request that has no validator),
      its result record and its handler returning `NumoResult<T>`. The page action's validator rejects a
      filter key the descriptor does not declare, and clamps page size to a sane maximum.
- [ ] **Step 5:** Write `ServiceDataController.cs`: routes `api/service-data`,
      `api/service-data/{resource}` and `api/service-data/{resource}/{id}`, dispatching only, with the
      tenant filter attached.
- [ ] **Step 6:** Write `Resources/PersonsResource.cs`: descriptor with the person columns and the
      `fullNamePart`, `email` and `isActive` filters; list, and detail by id. Establish here the two
      conventions the other resources copy - `HasMore` comes from fetching page N+1 and testing for
      emptiness, and `IPersonClient` failures arrive as exceptions rather than a result.
- [ ] **Step 7:** Establish the soft-delete convention that every later resource follows: deleted rows
      are excluded by default by simply not sending `IncludeDeletedSince`, which is a date rather than a
      flag, so including them means passing a far-past date. Expose it as a boolean filter that the
      resource translates, and populate `ResourceRow.DeletedAt` whenever the row carries one.
- [ ] **Step 8:** Register the resource and build. Run `dotnet build -t:Compile` and expect zero
      warnings; a CS0051 here means something in step 1 stayed internal.
- [ ] **Step 9:** Probe the catalogue: it lists `persons` with its columns and filters.
- [ ] **Step 10:** Probe paging: page 1 and page 2 at `pageSize=5`, and assert no row id appears in both.
      This is the probe that catches the paging defect the first design shipped.
- [ ] **Step 11:** Probe `HasMore`: true on a full page, false on the last page.
- [ ] **Step 12:** Probe sorting: ascending and descending on a sortable column change the first row.
      Confirm no request is sent with an empty `OrderBy`.
- [ ] **Step 13:** Probe the error paths: no tenant header, a malformed tenant, an unknown resource key,
      and an unknown record id. Each must produce a distinct problem-details response, not a 500 and not
      an empty page.
- [ ] **Step 14:** Probe search: `fullNamePart` narrows the result set. Report a count, never a name.
- [ ] **Step 15:** Commit: the contract plus the first resource.

---

### Task 3: The `employees` resource, with names and person-name search

**Files:**
- Create: `Features/ServiceData/Resources/EmployeesResource.cs`, `Resources/PersonNameLookup.cs`
- Modify: `Features/ServiceData/ServiceDataRegistration.cs`

**Interfaces:**
- Consumes: the Task 2 abstraction; `IEmployeeClient.GetEmployees(EmployeeFilter)` and
  `GetEmployee(Guid)`, both returning `Result<T>`; `IPersonClient` for the lookup.
- Produces: `PersonNameLookup` with two operations - resolve a set of person ids to display names, and
  pre-search person ids by name fragment with a cap. Task 4 uses both.

- [ ] **Step 1:** Write `PersonNameLookup.cs`. Id-to-name takes the distinct person ids of a page and
      issues one `GetPersons` call with `PersonIds`. Name-to-ids takes a fragment, calls `GetPersons`
      with `FullNamePart`, and returns at most 20 ids plus a flag saying whether it truncated. The cap
      exists because the client switches to `POST {endpoint}/search` above 1000 characters of query
      string and `/api/positions/view/search` does not exist; roughly 22 ids would 404.
- [ ] **Step 2:** Write `EmployeesResource.cs`. List: fetch the page, then one name lookup, projecting
      the name into a cell. The `personId` cell carries a `RecordLink` to `persons`. Declare a
      `personName` filter backed by the pre-search, and set `ResourcePage.Notice` when it truncates.
      Detail: the employee plus its person name, with relations to positions, absences and roles.
      Handle a failed `Result` as `DownstreamCallFailed` and a thrown `UnauthorizedException` as
      `DownstreamCallUnauthorized`.
- [ ] **Step 3:** Register it. Run `dotnet build -t:Compile` clean.
- [ ] **Step 4:** Probe the list and confirm a human name appears in the row and that the call count per
      page is two, not one per row.
- [ ] **Step 5:** Probe page 1 versus page 2 for id overlap, and `HasMore` on the last page.
- [ ] **Step 6:** Probe `personName` with a term matching more than 20 people: the notice appears, the
      response is 200, and nothing 404s.
- [ ] **Step 7:** Probe a detail record and follow the person link by hand.
- [ ] **Step 8:** Commit.

---

### Task 4: The `positions` resource

The awkward one, and the reason the abstraction allows several calls per method.

**Files:**
- Create: `Features/ServiceData/Resources/PositionsResource.cs`
- Modify: `Features/ServiceData/ServiceDataRegistration.cs`

**Interfaces:**
- Consumes: `IPositionClient.GetPositionsView(PositionFilter)`, `GetPosition(Guid)`;
  `IEmployeeClient.GetEmployee`, `IDepartmentClient.GetDepartment`, `IJobTitleClient.GetJobTitle`;
  `PersonNameLookup` from Task 3. `PositionFilter` offers `PersonIds`, `EmployeeIds`, `DepartmentIds`,
  `From`, `Till`, `Primary`, `LegalRelationId` - and no `Ids`.
- Produces: nothing new.

- [ ] **Step 1:** Write the list half against `GetPositionsView`, which embeds employee, department and
      job title, so those cells show names and carry links without extra calls. Add the person name via
      `PersonNameLookup`. Enum-typed properties project to names because the DTO types them as enums.
- [ ] **Step 2:** Write the detail half as a deliberate fan-out: `GetPosition(id)` plus employee,
      department and job title by id, plus the person name. There is no by-id view route and no `Ids`
      filter, so this is the only way; five calls for one developer-facing page is accepted.
- [ ] **Step 3:** Declare the filters that exist - `primary`, `from`, `till`, `departmentIds` - plus
      `personName` via the pre-search. Declare relations to employee, department, job title and person.
      Do not declare a positions-by-job-title reverse relation; no filter can express it.
- [ ] **Step 4:** Register it. Build clean.
- [ ] **Step 5:** Probe the list: names rather than GUIDs in the employee, department and job-title
      columns, and enum columns showing names rather than integers.
- [ ] **Step 6:** Probe page overlap and `HasMore`.
- [ ] **Step 7:** Probe one detail record and confirm all four links resolve.
- [ ] **Step 8:** Commit.

---

### Task 5: The `job-titles`, `departments` and `absences` resources

Three straightforward resources together, because each is the same shape and none is separately
rejectable in a meaningful way.

**Files:**
- Create: `Features/ServiceData/Resources/JobTitlesResource.cs`, `Resources/DepartmentsResource.cs`,
  `Resources/AbsencesResource.cs`
- Modify: `Features/ServiceData/ServiceDataRegistration.cs`

**Interfaces:**
- Consumes: `IJobTitleClient`, `IDepartmentClient` (including
  `GetDepartmentHierarchy(Guid, string?)`), `IAbsenceClient`.
- Produces: nothing new.

- [ ] **Step 1:** Write the job-titles resource: list, detail, and no filters beyond the defaults.
      Remember that `id` is not an orderable column here, so do not mark it sortable.
- [ ] **Step 2:** Write the departments resource: list with `activeFrom` and `activeTo` filters, detail
      with a parent link. Children come from `GetDepartmentHierarchy`, not from a parent filter, which
      does not exist. Relations to positions and roles.
- [ ] **Step 3:** Write the absences resource: list with `from`, `till`, `statuses` and `departmentIds`
      filters; relations to employee and department; enum columns for type and status.
- [ ] **Step 4:** Register all three. Build clean.
- [ ] **Step 5:** Probe each: catalogue entry, page overlap, `HasMore`, one detail record, one unknown
      id yielding `RecordNotFound`.
- [ ] **Step 6:** Probe the department children path specifically, since it is the one relation served
      by a different mechanism than the others.
- [ ] **Step 7:** Commit.

---

### Task 6: The `department-roles` resource, hand-rolled

The documented exception to the client-library rule. Keep it visibly exceptional.

**Files:**
- Create: `Features/ServiceData/Resources/DepartmentRolesResource.cs`
- Modify: `Features/ServiceData/ServiceDataRegistration.cs`

**Interfaces:**
- Consumes: `IServiceDiscoveryService` for the `Numo.Employee.Api` location; `IHttpClientFactory`.
- Produces: nothing new.

- [ ] **Step 1:** Register a named `HttpClient` whose base address is the `Numo.Employee.Api` location
      from service discovery. Assign rather than accumulate in `ConfigureHttpClient`, so a stray second
      registration degrades to a no-op.
- [ ] **Step 2:** Declare a local `DepartmentRole` record mirroring the route's payload, and a small
      envelope type for `{value, isSuccessful, errors}`. This exists because `DepartmentRoleDto` is
      `internal` in the library and no client method exposes the unfiltered route.
- [ ] **Step 3:** Write the resource: list with `departmentId`, `employeeId`, `active` and `roleType`
      filters; detail by id; relations to department and employee. It must add the `Numo-Tenant-Id`
      header itself, reading the current tenant, because it has none of the library's handler pipeline.
- [ ] **Step 4:** Add a comment at the top of the file saying in one or two lines why this resource is
      hand-rolled, so nobody copies it as the pattern.
- [ ] **Step 5:** Register it. Build clean.
- [ ] **Step 6:** Probe: catalogue entry, page overlap, `HasMore`, a detail record, an unknown id, and
      that the tenant header really is being sent - a missing header here fails differently than in the
      library-backed resources.
- [ ] **Step 7:** Commit.

---

### Task 7: Frontend foundation - models, API service, tenant store, routing

**Files:**
- Create: `ClientApp/src/app/features/service-data/api/service-data.model.ts`,
  `api/service-data-api.service.ts`, `state/tenant-id.store.ts`,
  `components/tenant-id-field/` (component, template, styles), `service-data.routes.ts`
- Modify: `ClientApp/src/app/app.routes.ts`, `ClientApp/src/app/core/layout/app-layout.ts`

**Interfaces:**
- Consumes: the backend routes from Tasks 2-6; `API_BASE_PATH` and `readProblemDetail` from
  `src/app/shared/api/`.
- Produces: the model types; `ServiceDataApiService` with a call per endpoint; `TenantIdStore` exposing
  the tenant id as a signal plus a setter that persists. Tasks 8 and 9 consume these.

- [ ] **Step 1:** Write the model file mirroring the backend shapes one-to-one. Where the backend has an
      enum, use a string union so a new kind is a compile error rather than a silent blank cell.
- [ ] **Step 2:** Write `TenantIdStore`: a signal initialised from `localStorage`, a setter that writes
      back, and a cleared state. Wrap every storage access in a try/catch - a private window or blocked
      site data makes the accessor itself throw.
- [ ] **Step 3:** Write `ServiceDataApiService` with the three calls. This is the only place the
      `Numo-Tenant-Id` header is attached, because a global interceptor would leak it onto the
      LaunchDarkly and ping calls and a feature-scoped interceptor does not exist in this app's wiring.
- [ ] **Step 4:** Write the `tenant-id-field` component: an input bound to the store, and a prompt state
      for when no tenant is set.
- [ ] **Step 5:** Write `service-data.routes.ts` with the two lazy routes, and wire the feature into
      `app.routes.ts`. Add the "Service data" nav item after "Service status".
- [ ] **Step 6:** Run `npm run build` and confirm it is clean and that a `service-data` lazy chunk
      appears. Run prettier on the new files only - the repo is not prettier-clean, so a broad run
      rewrites unrelated lines.
- [ ] **Step 7:** Commit.

---

### Task 8: The generic list page

**Files:**
- Create: `ClientApp/src/app/features/service-data/components/generic-record-table/`,
  `components/resource-filter-bar/`, `pages/service-data-page/`

**Interfaces:**
- Consumes: Task 7's models, API service and tenant store.
- Produces: the list page at `/service-data/:resource`. Task 9's relation buttons navigate into it with
  a filter in the query string, so the page must read its filters from the URL and not from local state
  alone.

- [ ] **Step 1:** Write `generic-record-table`: headers from `ColumnDescriptor`, a sort control only on
      columns marked sortable, and a cell rendered as a `routerLink` when it carries a link and as text
      otherwise. A soft-deleted row shows a badge.
- [ ] **Step 2:** Write `resource-filter-bar`: one input per `FilterDescriptor`, typed by its kind, and
      an apply action that writes the filters into the URL query string.
- [ ] **Step 3:** Write the list page: resource picker from the catalogue, the filter bar, the table, the
      notice when the backend sets one, and Previous/Next buttons. Do not use PrimeNG's paginator - it
      derives its state from `ceil(totalRecords / rows)` and with no total both arrows stay disabled.
- [ ] **Step 4:** React to route parameter changes rather than loading in the constructor: Angular reuses
      the component instance when only `:resource` changes, so constructor-time loading will not reload.
      Reset filters and page when the resource changes.
- [ ] **Step 5:** With no tenant set, render the prompt and fire no requests. Surface a backend failure
      through `readProblemDetail`, distinguishing the tenant, unauthorized and downstream cases.
- [ ] **Step 6:** Run `npm run build` clean, then exercise all seven resources in a browser against the
      test tenant: paging forward and back, one sort, one filter, and the empty and error states.
- [ ] **Step 7:** Commit.

---

### Task 9: The generic detail page and relation navigation

**Files:**
- Create: `ClientApp/src/app/features/service-data/components/generic-record-detail/`,
  `pages/service-data-record-page/`

**Interfaces:**
- Consumes: Task 7's models and API service; the list page route from Task 8.
- Produces: the detail page at `/service-data/:resource/:id`.

- [ ] **Step 1:** Write `generic-record-detail`: every `FieldValue` rendered by kind, a field with a link
      becoming a `routerLink` to the target record, and a button per `RelationDescriptor` that navigates
      to the list page with the relation's filter in the query string.
- [ ] **Step 2:** Write the detail page: load by route parameters, handle `RecordNotFound` as a legible
      empty state rather than an error banner, and offer a back path to the list.
- [ ] **Step 3:** Run `npm run build` clean.
- [ ] **Step 4:** Walk every declared relation in the browser: person to employees, employee to
      positions and absences and roles, department to positions and roles and children, position to
      employee and department and job title and person. Each must land on a filtered list, and the
      browser back button must return.
- [ ] **Step 5:** Commit.

---

### Task 10: Documentation

Not an afterthought: the repo keeps its decisions in these files, and two of them currently say things
this feature makes untrue.

**Files:**
- Modify: `docs/Purpose.md`, `docs/Architecture.md`, `CLAUDE.md`

- [ ] **Step 1:** In `docs/Purpose.md`, mark planned feature 3 built, naming the slice and the route,
      and update the roll-up sentence about which features are built.
- [ ] **Step 2:** Answer that document's open question about what "generic components from JSON" means
      in one or two lines: an explicit display descriptor from the backend, as this slice does, rather
      than column inference in the frontend.
- [ ] **Step 3:** Add the standing constraints this feature discovered: the tenant header requirement,
      the `AllowUnauthorizedApiCalls` flag and why it exists, the absence of any total count, the
      `OrderBy` syntax and its empty-value trap, and that names live only on `PersonDto`.
- [ ] **Step 4:** In `docs/Architecture.md`, amend the note saying `INumoCurrentTenantService` was
      deliberately avoided: a UI-supplied downstream tenant id is a different thing from entity
      tenancy, which is still absent. Record that `Features/ServiceData/` is the first slice that must
      gain `[Authorize]` when authentication lands, and that it reads personal data for a
      caller-supplied tenant with no authentication in the meantime.
- [ ] **Step 5:** Add the slice to the layout list in `CLAUDE.md`, one line, and note the
      `Resources/` subfolder convention.
- [ ] **Step 6:** Check every touched file stays within 100 columns and keeps CRLF endings.
- [ ] **Step 7:** Commit.

---

### Task 11: Verification sweep

The spec's verification plan, run end to end against the finished feature rather than per task, because
several of its items are cross-resource.

- [ ] **Step 1:** `dotnet build` clean with zero warnings; `npm run build` clean.
- [ ] **Step 2:** For all seven resources: page 1 versus page 2 at `pageSize=5` with no id overlap.
- [ ] **Step 3:** For all seven: `HasMore` true on a full page and false on the last.
- [ ] **Step 4:** For every column marked sortable: ascending and descending differ. Confirm by log or
      proxy that no request carried an empty `OrderBy`.
- [ ] **Step 5:** For all seven: one detail record renders, and an unknown GUID yields `RecordNotFound`
      rather than a 500 or a blank page.
- [ ] **Step 6:** Every declared relation followed once, in both directions where declared.
- [ ] **Step 7:** Person-name search on employees and positions, including a term matching more than 20
      people: the notice appears and nothing 404s.
- [ ] **Step 8:** Enum columns show names, not integers, on absences and positions.
- [ ] **Step 9:** No tenant, a malformed tenant, and a well-formed unknown tenant each give a
      distinguishable message rather than a bare empty grid.
- [ ] **Step 10:** Confirm no write request was ever issued to either service, and that no log line or
      probe output in this work contains personal data.
- [ ] **Step 11:** Confirm the user's own running instance was never disturbed: the app under test ran on
      a spare port with `--no-launch-profile`, and only those processes were stopped.
- [ ] **Step 12:** Report the results, naming anything that could not be verified rather than implying
      full coverage.
