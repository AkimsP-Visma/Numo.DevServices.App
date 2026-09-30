## Why

`Features/ServiceData/` and its two frontend nav entries ("Personnel Browser" and
"DataIntegration Browser") are already built and in daily use, but OpenSpec was only just
initialized in this repo, so no spec exists for them yet. This change adds no behavior: it
retroactively documents the feature as it exists today, by reading the implementation and the
design docs it was built from, so that OpenSpec has an accurate baseline to diff future changes
of this feature against. Without this baseline, the next real change to service data browsing
would have nothing to propose a delta against.

## What Changes

- Document the descriptor-driven resource abstraction (`IServiceDataResource`,
  `ResourceDescriptor`, `ColumnDescriptor`, `FilterDescriptor`, `ResourceQuery`, `ResourcePage`,
  `ResourceRecord`, `RelationDescriptor`) that lets the frontend render any resource generically.
- Document the two-section catalogue (Personnel, DataIntegration) served from one backend slice
  and picked by a `section` query parameter that two nav entries set.
- Document the tenant-header requirement, which differs per resource (`RequiresTenant`), and the
  "no request fires until a tenant id is entered" rule for tenant-requiring resources.
- Document the two paging strategies (fetch-page-N+1 for Person/Employee, fetch-all-and-slice for
  DataIntegration) and the continuation-token, single-batch paging of the execution-step dataset.
- Document the picker-exclusion rules: `IsReachableOnlyByRelation` resources and any resource with
  an `IsRequired` filter are both hidden from the top-level picker.
- Document the inline relation-panel mechanism (`related-records-panel`): an ordinary relation
  auto-loads, a gated relation stays behind a button, and every panel links out to the full list
  page.
- Document the credentials/certificates/dataset gating (secrets and personal data, relation-only,
  fetched on demand) versus the parameters relation (non-secret, auto-loading).
- Document the legal-relation-as-two-relations decision for Position/Absence records, since no
  `LegalRelation` entity exists anywhere to link to.
- Document the ordering, soft-delete and person-name-resolution rules that make the Person/Employee
  grids legible, and the lack of a client library for any DataIntegration resource or for
  `department-roles`.

This is documentation only. No source file under `src/` is touched.

## Capabilities

### New Capabilities
- `service-data-browsing`: the full descriptor-driven service data browsing feature - resource
  catalogue and sections, tenant handling, paging strategies, filtering, sorting, relations and
  inline relation panels, and the security-driven exclusions (gated resources, required filters,
  legal relation handling) that shape what is browsable and how.

### Modified Capabilities
- None. No existing `openspec/specs/` capability exists yet for this feature.

## Impact

- Affected code: none (documentation-only change under `openspec/`).
- Affected systems: `src/Numo.DevServices.Api/Features/ServiceData/` (backend slice) and
  `src/Numo.DevServices.Api/ClientApp/src/app/features/service-data/` (frontend feature), as read
  today - this proposal changes neither.
