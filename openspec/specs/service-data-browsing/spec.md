# service-data-browsing Specification

## Purpose
A descriptor-driven, generic browsing UI over data the main Numo product doesn't expose, spanning
two sections from one backend slice: Personnel (Person/Employee data) and DataIntegration (the
Configuration API's clients, pipelines, connections and related records). Every resource declares
its columns, filters and relations once in C#; the frontend renders grids, detail pages and inline
relation previews from that data alone, adding per-resource frontend code only where a feature
genuinely needs its own shape. Sensitive resources (connection credentials/certificates, an
execution step's real dataset) are reachable only on demand via a relation button, never listed.
## Requirements
### Requirement: Descriptor-driven resource catalogue
The system SHALL expose a compile-time catalogue of browsable resources, each implementing
`IServiceDataResource` and declaring a `ResourceDescriptor` (key, label, service name, section,
columns, filters, `RequiresTenant`, `IsReachableOnlyByRelation`). `GET /api/service-data` SHALL
return every registered descriptor with no tenant required and no downstream call made, since
descriptors are static metadata. The frontend SHALL render its resource picker, grid columns and
filter bar entirely from this data, adding no per-resource frontend code for a resource that needs
no shape the descriptor cannot already express.

#### Scenario: Catalogue loads before a tenant id exists
- **WHEN** the frontend requests `GET /api/service-data` with no tenant id ever having been entered
- **THEN** the request succeeds and returns every resource's descriptor, because the catalogue is
  compile-time metadata and calls no downstream service

#### Scenario: A resource key not in the catalogue is rejected
- **WHEN** a page or record request names a resource key that no registered `IServiceDataResource`
  declares
- **THEN** the handler fails with the `UnknownResource` error rather than attempting a downstream
  call

### Requirement: Two browsing sections from one backend slice
Each `ResourceDescriptor` SHALL declare a `Section` (`Personnel` or `DataIntegration`). The
frontend SHALL offer two nav entries, "Personnel Browser" and "DataIntegration Browser", each
setting a `section` query parameter that filters the resource picker to that section's resources.
A single route prefix (`/service-data/:resource`) SHALL serve both sections, so a cell or relation
link MAY cross sections without any lookup of which section a target resource belongs to.

#### Scenario: Personnel Browser nav entry filters the picker
- **WHEN** a user opens the "Personnel Browser" nav entry
- **THEN** the resource picker offers only resources whose descriptor declares `Section: Personnel`

#### Scenario: A relation link crosses sections
- **WHEN** a DataIntegration record links to a resource in a different section (or vice versa)
- **THEN** the link navigates successfully, because the route and the record-fetch mechanism do not
  depend on section

### Requirement: Per-resource tenant requirement
Each `ResourceDescriptor` SHALL declare `RequiresTenant` (default `true`). Personnel resources
(Person and Employee service data) SHALL require a tenant id; DataIntegration resources SHALL
declare `RequiresTenant: false`, since the Configuration API was verified to answer anonymously.
`TenantIdActionFilter` SHALL demand and set a tenant id only when the resolved resource's
descriptor requires one, short-circuiting with a 400 `TenantIdMissing` failure otherwise.

#### Scenario: A DataIntegration resource is browsable with no tenant header
- **WHEN** a request for `di-clients` (or any other DataIntegration resource) carries no
  `Numo-Tenant-Id` header
- **THEN** the request succeeds, because its descriptor declares `RequiresTenant: false`

#### Scenario: A Personnel resource rejects a missing or malformed tenant id
- **WHEN** a request for `persons`, `employees`, or any other tenant-requiring resource carries no
  `Numo-Tenant-Id` header, or a value that does not parse as a non-empty GUID
- **THEN** the request fails with a 400 response carrying the `TenantIdMissing` error id, and no
  downstream call is made

#### Scenario: No request fires until a tenant id is entered
- **WHEN** the frontend has not yet had a tenant id entered and the active resource's descriptor
  requires one
- **THEN** the page fires no request at all and shows a prompt, rather than sending a request that
  would otherwise be misread as "no data" when a well-formed but unrecognized tenant id returns an
  empty list

### Requirement: Page-index paging with no total count
For resources backed by a paginating service route (Person, Employee), the system SHALL derive
`ResourcePage.HasMore` by fetching page `N+1` at the same `PageSize` and testing it for emptiness,
because neither service reports a total count anywhere in its response envelope or headers, and
inflating `PageSize` to detect a next page would shift the server's window and silently skip a row
at every page boundary.

#### Scenario: HasMore is derived from a peek at the next page
- **WHEN** a full page of `PageSize` rows is returned for a Person or Employee resource
- **THEN** the system additionally fetches page `N+1` at the same `PageSize` and sets `HasMore` to
  whether that fetch returned any rows

#### Scenario: A short page is already the last one
- **WHEN** a fetched page returns fewer rows than `PageSize`
- **THEN** `HasMore` is `false` and no peek at page `N+1` is made

### Requirement: Unpaged fetch-and-slice paging for DataIntegration
For every DataIntegration resource, whose routes take no paging parameters at all, the system SHALL
fetch the whole filtered, unpaged list once and slice it in memory to the requested page and page
size. This is only considered correct because these sets are configuration-sized (clients,
pipelines, connectors, connections and similar), not the 1300+-row Person/Employee tables that
require fetch-per-page.

#### Scenario: A DataIntegration resource is sliced in memory
- **WHEN** a page is requested for `di-pipelines` (or another unpaged DataIntegration resource)
- **THEN** the system fetches the complete list once and returns the in-memory slice for the
  requested page, with `HasMore` computed from the total count already in hand

### Requirement: Continuation-token paging for the execution step dataset
The `di-execution-step-dataset` resource SHALL page by the Configuration API's own opaque
continuation token rather than by page index, since no index-based request ("page 3") can be
formed against it. The system SHALL fetch exactly one batch per request and never claim `HasMore`;
when the service's own token indicates more records exist, `ResourcePage.Notice` SHALL say so
instead of the grid offering a Next control it cannot honor.

#### Scenario: A dataset page never claims more pages exist
- **WHEN** a page of `di-execution-step-dataset` is requested and the service's response carries a
  non-null continuation token
- **THEN** `ResourcePage.HasMore` is `false` and `ResourcePage.Notice` states that the dataset has
  more records than are shown

#### Scenario: A dataset record has no fixed columns
- **WHEN** a dataset record's detail is opened
- **THEN** every key in the record's data dictionary is rendered as its own field, because a
  dataset record's schema is whatever the source connector produced and cannot be declared as a
  fixed set of `ColumnDescriptor`s

### Requirement: Opt-in, narrow-syntax column ordering
The system SHALL treat a column as orderable only when its `ColumnDescriptor.IsSortable` is `true`,
a fact established per column by probing the live service, because a service silently ignores an
order on a column it does not mark orderable rather than rejecting it. `OrderBy` SHALL be omitted
entirely from a downstream
request when no sort is requested, and SHALL never be sent as an empty value, because an empty
`OrderBy=` causes the request to fail.

#### Scenario: Sorting on a column not marked sortable is rejected before any call
- **WHEN** a page request asks to sort by a column whose descriptor has `IsSortable: false`
- **THEN** the request fails validation and no downstream call for that page is made

#### Scenario: No sort omits OrderBy entirely
- **WHEN** a page request specifies no sort column
- **THEN** the downstream request carries no `OrderBy` parameter at all

### Requirement: Filter validation against declared descriptors
A `ResourceQuery`'s filters SHALL only ever contain keys the resource's `ResourceDescriptor`
declared; the page and record validators SHALL reject any other key and any value malformed for
its `FilterKind` before a resource runs. A `FilterDescriptor` marked `IsRequired` (a nested
resource's parent-id filter) SHALL be enforced as present by the page validator, so a nested
resource is never asked to browse without the parent id its route needs.

#### Scenario: An undeclared filter key is rejected
- **WHEN** a page or record request carries a filter key the resource's descriptor does not declare
- **THEN** the request fails validation and no downstream call is made

#### Scenario: A required filter missing from a list request is rejected before the resource runs
- **WHEN** a page request for a resource with an `IsRequired` filter (e.g. `di-client-resources`'s
  `clientId`) omits that filter
- **THEN** the request fails validation with a message naming the missing filter, and
  `GetPageAsync` is never invoked

#### Scenario: A required filter missing from a detail request is reported by the resource itself
- **WHEN** a detail request for a nested resource whose `GetByIdAsync` needs a parent id omits that
  filter (the record route does not enforce `IsRequired` uniformly, since not every resource's
  detail needs the filter its list requires)
- **THEN** the resource itself fails with `ServiceDataErrors.RequiredFilterMissing` rather than an
  unhandled exception reaching the caller as a bare 500

### Requirement: Bounded GuidList filters
A `FilterKind.GuidList` filter SHALL carry several ids in one comma-delimited value, capped at
`ResourceQueryFilters.MaxListValues` (4) values by the page validator, so that an overlong list is a
stated 400 response rather than a downstream request a client library silently turns into a `POST
.../search` (which either breaks the GET-only constraint or, for positions, hits a route that does
not exist at all).

#### Scenario: A GuidList filter over the cap is rejected
- **WHEN** a page request's `employeeIds` or `departmentIds` filter carries more than 4 valid,
  non-empty GUIDs
- **THEN** the request fails validation rather than reaching a downstream client

#### Scenario: An all-zero-GUID list is rejected, not silently emptied
- **WHEN** a GuidList filter's value parses to nothing but empty GUIDs
- **THEN** the request fails validation, because the services ignore an empty id array and would
  otherwise answer with the whole table instead of the intended empty result

### Requirement: Picker excludes unbrowsable-standalone resources
A resource SHALL be excluded from the frontend's top-level resource picker (`pickerResources`) when
either its descriptor declares `IsReachableOnlyByRelation: true`, or it declares any filter with
`IsRequired: true`. Both conditions independently mean the resource cannot be meaningfully browsed
without a value only a relation link can supply, so opened directly from the picker it would show
nothing or fail validation immediately.

#### Scenario: A gated resource is absent from the picker
- **WHEN** the resource picker is built from the catalogue
- **THEN** `di-connection-credentials`, `di-connection-certificates` and
  `di-execution-step-dataset` (all `IsReachableOnlyByRelation`) do not appear as picker entries

#### Scenario: A nested resource with a required filter is absent from the picker
- **WHEN** the resource picker is built from the catalogue
- **THEN** `di-client-resources`, `di-pipeline-resources`, `di-pipeline-executions`,
  `di-execution-steps` and `di-connection-parameters` (each declaring a required parent-id filter)
  do not appear as picker entries, even though none of them is `IsReachableOnlyByRelation`

### Requirement: Relations render inline, not as navigate-away links
A relation SHALL render as an inline preview panel (`related-records-panel`), built from a
`RelationDescriptor` (label, target resource key, a `Filters` dictionary keyed the same way as
`ResourceQuery.Filters`), under
the parent record, showing up to 10 rows, rather than a tab that would open to an empty grid
demanding the same filter the parent record already supplies. Every panel SHALL link out to the
resource's full list page (reachable by direct URL even though not picker-tabbed) for paging,
sorting or filtering beyond the preview.

#### Scenario: An ordinary relation auto-loads
- **WHEN** a parent record's detail page renders a relation into a resource that is not
  `IsReachableOnlyByRelation` (e.g. a client's "Resources" relation)
- **THEN** the relation's preview panel fetches and displays its first page automatically, with no
  user action required

#### Scenario: A gated relation stays behind a button
- **WHEN** a parent record's detail page renders a relation into an `IsReachableOnlyByRelation`
  target (credentials, certificates, or a dataset)
- **THEN** the panel shows a button instead of fetching automatically, and the fetch happens only
  after that button is pressed

#### Scenario: A relation needing two parent ids is expressible
- **WHEN** an execution step's "Dataset" relation is rendered
- **THEN** its `RelationDescriptor.Filters` carries both `executionId` and `stepId`, since the
  target route needs both parent ids at once and a single filter-key/value pair could not express
  that

### Requirement: Credentials and certificates are gated; parameters are not
`di-connection-credentials` and `di-connection-certificates` SHALL declare
`IsReachableOnlyByRelation: true` and SHALL never appear in any list, reachable only via the
"Credentials" or "Certificates" relation on a connection record, fetched only when that relation's
button is pressed. `di-connections` itself SHALL never send an `expand` parameter that would embed
either into a list response, and its own `Connection` record SHALL carry no property for either, so
that even an unintended expansion cannot leak a secret into a list cell. `di-connection-parameters`
SHALL NOT be gated: verified live, every connection's `parameters` dictionary carries only plain,
non-secret configuration (such as a `RestUrl`), so its relation auto-loads like any ordinary one.

#### Scenario: Credentials are only reachable via a relation button
- **WHEN** a user wants to see a connection's credentials
- **THEN** the only path is opening that connection's record and pressing the "Credentials"
  relation button; `di-connection-credentials` never appears in the resource picker or any other
  list

#### Scenario: A connection list response cannot carry credentials or certificates
- **WHEN** `di-connections` fetches its list
- **THEN** no `expand` parameter is sent and the `Connection` record used to deserialize the
  response has no property that could hold a credential or certificate value

#### Scenario: Parameters load automatically
- **WHEN** a connection record's "Parameters" relation is rendered
- **THEN** it auto-loads like any ordinary relation, because parameters are verified non-secret
  configuration

### Requirement: Execution step dataset is gated for personal-data reasons
`di-execution-step-dataset` SHALL declare `IsReachableOnlyByRelation: true` and SHALL be reachable
only via the "Dataset" relation on an execution step record, because its content is whatever a
pipeline's source connector extracted and has been confirmed, by live probe, to include real
person-linked fields for at least one real pipeline (an HR/absence pipeline). This is an
independent justification for gating from the credentials/certificates case (secrets), both
recorded as reasons this slice needs `[Authorize]` once authentication exists.

#### Scenario: A dataset is never fetched except on demand
- **WHEN** an execution step's detail page is rendered
- **THEN** the dataset relation is not fetched automatically; it is fetched only after its button
  is pressed

### Requirement: Legal relation renders as two filtered relations, not a link
A position's or absence's non-null `LegalRelationId` SHALL render as two `RelationDescriptor`s
("Positions" and "Absences", each filtered by that id) rather than as a `RecordLink` to a
nonexistent resource or as inert text. This is required because no `LegalRelation` entity or
client exists anywhere in `Numo.Employee.Lib` - `PositionDto` and `AbsenceDto` each carry only a
bare `LegalRelationId`, filterable on `PositionFilter` and `AbsenceFilter` but resolvable to no
record of its own, not even a name.

#### Scenario: A position's legal relation id yields two relations
- **WHEN** a position record with a non-null `LegalRelationId` is opened
- **THEN** its relations include "Positions" and "Absences", each filtered by `legalRelationId`
  equal to that value, and no `RecordLink` is produced for the id

#### Scenario: A null legal relation id yields no such relations
- **WHEN** a position or absence record has a null `LegalRelationId`
- **THEN** no "Positions"/"Absences" legal-relation entries appear among its relations

### Requirement: Cross-service name resolution for employees and positions
The `employees` and `positions` resources SHALL resolve a display name via one batched
`IPersonClient` lookup per page (not one call per row), because `EmployeeDto` carries no human
name (only `id, deletedAt, personId, code, email, phone`). They SHALL additionally expose a
`personName` pre-search filter that resolves a name fragment to person ids in the Person service
before filtering Employee-service data, since the Employee and Position services cannot search by
name themselves.
Each resource's pre-search id cap SHALL be sized to its own downstream query-string budget
(`employees`: 18 ids; `positions`: 8 ids), and exceeding the cap SHALL produce a truncation
`Notice` rather than a silent drop or a fallback to a `POST .../search` (which 404s for positions).

#### Scenario: A page of employees resolves names with one batched call
- **WHEN** a page of `employees` is fetched
- **THEN** exactly one `IPersonClient.GetPersons` call resolves the names for every distinct
  `personId` on that page, not one call per row

#### Scenario: A person-name search exceeding the cap reports truncation
- **WHEN** a `personName` filter on `employees` matches more than 18 distinct people
- **THEN** the response's `Notice` states that only the first 18 matching people's employees are
  shown

### Requirement: Department children via hierarchy, not a reverse relation
Because `DepartmentFilter` has no `ParentId`, a department record's direct children SHALL be
obtained from `GetDepartmentHierarchy` (which returns the whole subtree) rather than expressed as a
`RelationDescriptor`, and SHALL render as linked fields on the record rather than a relation, since
a relation would have to pass every child id through the slice's list-value cap and silently drop
the rest. A department with more children than the record can list SHALL state the omitted count
rather than presenting a silently truncated list as complete.

#### Scenario: A department's direct children appear as linked fields
- **WHEN** a department record with children is opened
- **THEN** each direct child (a node in the hierarchy result whose `ParentId` equals the opened
  department's id) appears as its own linked "Child" field, not as a relation

#### Scenario: Excess children are counted, not silently dropped
- **WHEN** a department has more direct children than the record's listing limit
- **THEN** the record states how many additional children were not listed

### Requirement: Stable, branchable error ids
Every failure this slice can produce SHALL carry a stable `NumoError` id (`UnknownResource`,
`TenantIdMissing`, `RecordNotFound`, `DownstreamCallFailed`, `DownstreamCallUnauthorized`,
`RequiredFilterMissing`) so a caller can branch on the id rather than parsing the message.
`DownstreamCallUnauthorized` SHALL be distinguished from `DownstreamCallFailed` because the client
libraries throw client-side, before any request is sent, when the app lacks an authenticated
principal and `FeatureManagement:AllowUnauthorizedApiCalls` is not set - there is no HTTP status to
report in that case.

#### Scenario: A missing record yields RecordNotFound, not a 500
- **WHEN** a detail request names a valid resource and a well-formed id the downstream service has
  no record for
- **THEN** the response carries the `RecordNotFound` error id, not an unhandled exception

#### Scenario: A client-side authorization rejection is distinguished from a downstream failure
- **WHEN** a client library throws `UnauthorizedException` before sending a request
- **THEN** the response carries `DownstreamCallUnauthorized`, distinct from `DownstreamCallFailed`,
  and carries no status code

### Requirement: No resource is a pass-through to an arbitrary path
Each resource SHALL be bound to a fixed path (or fixed set of paths) on a service named by a
configuration key, resolved through a closed, compile-time-registered catalogue - `{resource}` in
the route is a key into that catalogue, never a caller-supplied path forwarded verbatim. This keeps
the slice consistent with the standing "not a reverse proxy" decision recorded in
`docs/Purpose.md`.

#### Scenario: An unregistered resource key never reaches a downstream service
- **WHEN** a request names a resource key with no matching registered `IServiceDataResource`
- **THEN** no downstream HTTP call is made at all; the handler fails with `UnknownResource`

