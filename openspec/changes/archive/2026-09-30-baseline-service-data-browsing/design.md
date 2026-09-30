## Context

`Features/ServiceData/` browses live data from other Numo services (Person, Employee,
DataIntegration Configuration) for developer use. It is built and shipped; this design document
records the architectural decisions already made in the code and in
`docs/superpowers/specs/2026-09-21-service-data-browsing-design.md`, `docs/Purpose.md` and
`docs/Architecture.md`, so future changes to this feature have a record of *why* it looks the way
it does rather than having to re-derive it from the diff.

## Goals / Non-Goals

**Goals:**
- Record why no DataIntegration resource, and no Personnel `department-roles` resource, uses a
  client library.
- Record the two paging strategies and why each is correct for its data source.
- Record the tenant-header design and why it is per-resource rather than global.
- Record the `IsReachableOnlyByRelation` + required-filter picker-exclusion rule.
- Record the inline-relation-panel design and its security rationale for gated relations.
- Record the legal-relation-as-two-relations decision.
- Record the credentials/certificates/parameters gating split.

**Non-Goals:**
- Proposing any new behavior. This document describes the system as built.
- Authentication/authorization design - `[Authorize]` is explicitly not wired yet (see
  `docs/Architecture.md`); this feature is the reason it is needed next, not a design for it.

## Decisions

**No client library for any DataIntegration resource.** `IConfigurationClient` (25 methods,
enumerated by reflection since it is undocumented) is built for point lookups by name - it has no
list method for clients, pipelines, connectors, connections, client resources, pipeline resources,
pipeline executions or execution steps, and no by-id method for the nested certificates or
credentials routes. Building a browsing feature on it would mean bolting list semantics onto an
API that does not have them. Instead every DataIntegration resource reads the Configuration API
directly through one shared hand-rolled `HttpClient`
(`Resources/DataIntegrationConfigurationApi.cs`), verified live to answer with plain JSON (no
envelope) and need no tenant header. `Numo.DataIntegration.Connectors.Lib` is not referenced at
all because none of the chosen resources need the Connectors API.

**No client library for `department-roles` either, but for a different reason.**
`DepartmentRoleDto` is `internal` in `Numo.Employee.Lib` and `IDepartmentClient` exposes no method
for the unfiltered roles route - this is a gap in that specific library, not a general problem
with the Employee service's libraries (every other Personnel resource uses one). This resource
therefore owns its own `HttpClient`, its own local `DepartmentRole` record, its own response
envelope, and its own `Numo-Tenant-Id` header (which a client library's handler pipeline would
otherwise attach). Parsing the envelope by hand buys one capability the libraries could not:
recognizing "record not found" from the error's structured metadata (`ParamName`/`ParamValue`)
rather than from the wording of a message.

**Two paging strategies, chosen per data source's actual capability, not by preference.**
Person/Employee routes are genuinely page-indexed but report no total count anywhere - not in the
envelope, not in a header. `ResourcePageBuilder.BuildAsync` fetches page N+1 at the same page size
and tests it for emptiness to derive `HasMore`; this is one extra downstream call per page view and
is the only arithmetically correct way to know if another page exists on this kind of API (asking
for `pageSize + 1` rows was tried and rejected: the server's window is a function of `PageSize`, so
inflating it shifts page 2's start and silently drops a row at every boundary).
`ResourcePageBuilder.BuildUnpagedAsync` handles every DataIntegration route instead, because none
of them page at all - it fetches the whole list once and slices in memory. This is only honest
because DataIntegration's sets are configuration-sized (a handful of clients, pipelines,
connectors), not the 1300+-row Person/Employee tables that make a fetch-per-page mandatory there.
`di-execution-step-dataset` uses neither builder: its route pages by opaque continuation token, not
index, so there is no way to express "page 3." It fetches exactly one batch and stops -
`ResourcePage.HasMore` is always false, and `Notice` says more records exist (from the service's own
token) rather than the grid offering a Next it cannot honor.

**Tenant header is per-resource (`RequiresTenant`), not a blanket slice-level rule.** Person and
Employee answer anonymously given `Numo-Tenant-Id` and 401 without it; the DataIntegration
Configuration API was verified live to need no tenant header at all. A single slice-wide filter
would either force a tenant id on data that does not need one, or (worse) silently omit it for data
that does. `TenantIdActionFilter` reads the resolved resource's `Descriptor.RequiresTenant` before
deciding whether to demand and set a tenant id, short-circuiting with 400 if one is required and
missing/malformed. The frontend mirrors this: `service-data-page.ts`'s `requiresTenant` computed
signal gates whether a request fires at all, so a tenant-requiring resource never fires a request
with no tenant id typed in (which would otherwise read as "no data" rather than "nothing asked
yet" - the services answer an unrecognized but well-formed tenant with an empty list, not an
error).

**Picker exclusion: `IsReachableOnlyByRelation` OR any `IsRequired` filter.** The first rule alone
was insufficient: a resource can need a parent id supplied only by a relation
(`di-client-resources` and its siblings) without being marked `IsReachableOnlyByRelation` itself
(that flag is reserved for resources gated for security reasons - credentials, certificates,
dataset). Both conditions independently make a resource unbrowsable standalone, so
`service-data-page.ts`'s `pickerResources` computed signal filters out a resource matching either
one, not just the first.

**Inline relation panels, not navigate-away links, with a security-driven split in default
behavior.** `related-records-panel` embeds a relation's preview (10 rows) directly under the parent
record rather than a tab that would open empty and immediately demand the same filter the parent
already supplies. An ordinary relation (e.g. `di-client-resources` from a client record) loads
automatically on render. A relation into an `IsReachableOnlyByRelation` target does not: it renders
a button that must be pressed to fetch, so credentials, certificates and dataset records are never
fetched speculatively - only on an explicit user action. Every panel still links out to the full
list page for paging/sorting/filtering beyond the 10-row preview, since that capability should not
be lost just because the preview is capped.

**Legal relation is two relations, not a link, because there is no entity to link to.**
`Numo.Employee.Lib` has no `LegalRelation` type or client anywhere; `PositionDto` and `AbsenceDto`
each carry a bare `LegalRelationId` and their filters (`PositionFilter.LegalRelationId`,
`AbsenceFilter.LegalRelationId`) can filter by it, but no route returns a legal relation's own data,
not even a name. A `RecordLink` would point at a resource key that does not exist; inventing a
standalone "legal relations" resource would fabricate a browsable entity the platform does not
have. The chosen shape follows the same shared-foreign-key pattern used elsewhere in this slice: the
id becomes two `RelationDescriptor`s (filtered "Positions" and "Absences" grids), which is honest
about what is actually knowable.

**Credentials, certificates and parameters split by secrecy, not by mechanism.** All three are read
through the same `expand=...` query parameter on a connection's detail route. Credentials
(plaintext values) and certificates (certificate material) are `IsReachableOnlyByRelation`: never
listed, fetched only when a relation button is pressed. Parameters are not: verified live against
test.numo.lv, every connection's `parameters` dictionary carries plain configuration (e.g.
`RestUrl`), never a secret, so that relation auto-loads like any other. `di-connections` itself
never sends `expand`, and its own `Connection` record has no property for credentials or
certificates, so even an unexpected expansion by that route could not leak a secret into a list
cell - a structural guarantee, not just a policy of not asking for it.

**The execution-step dataset is gated for a different, independently-discovered reason.** Unlike
the rest of the DataIntegration section (pure configuration, no personal data), a live probe during
this resource's build found an HR/absence pipeline whose dataset batch carried real person-linked
fields (`ERS_PK_PERSSH`, `ABS_DAT_FROM`, etc.) - the dataset is whatever a pipeline extracted, and at
least one real pipeline extracts personal data. It is gated the same way as credentials/certificates
(`IsReachableOnlyByRelation`) even though the *reason* is different (personal data vs. secrets), and
this is named as a second, independent justification in `docs/Architecture.md` for why the slice
needs `[Authorize]`.

## Risks / Trade-offs

- [DataIntegration's unpaged fetch-all approach does not scale if any of those routes grows past
  "configuration-sized"] → Accepted for now; the code comments this tradeoff explicitly and it was
  only ever meant for small sets.
- [The execution-step dataset's `GetByIdAsync` re-fetches a deterministic first batch and searches
  it, so a record past that batch cannot be opened directly] → Documented limitation, not fixed;
  there is no by-id route to use instead.
- [No `[Authorize]` yet, while this slice reads real personal data (Person/Employee) and, via two
  independently-discovered gated resources, real secrets and real personal data from
  DataIntegration] → Accepted only because the app is network-internal and developer-only;
  `docs/Architecture.md` names this slice as the one that must gain `[Authorize]` first.

## Migration Plan

None - this document records existing behavior. No code changes accompany it.

## Open Questions

None specific to this baseline; open questions about the feature's future (service registry
location, replacing Swagger UI, etc.) are tracked in `docs/Purpose.md` and are out of scope for a
documentation baseline of already-shipped behavior.
