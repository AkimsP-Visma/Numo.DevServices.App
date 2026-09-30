## Why

Two of the five backfilled capability specs independently restate the same platform-wide rule in
their own words: `service-data-browsing` and `api-catalog-browser` each declare, separately, that
no endpoint forwards an arbitrary caller-supplied path to a service - the same underlying decision,
documented twice, free to drift apart the next time either capability changes. `docs/Purpose.md`
also carries this decision (and "prefer a service's own client library") as prose bullets with no
spec-level home at all. This change gives cross-feature platform rules exactly one place to live -
a new `platform-conventions` capability, discoverable the same way every other capability is,
since it lives in the same `openspec/specs/` tree - and removes the duplication rather than leaving
two copies to maintain.

## What Changes

- Add a `platform-conventions` capability covering: no endpoint is a pass-through to an arbitrary
  path, prefer a service's own client library over a hand-rolled HTTP client, and failures carry
  stable, branchable error ids.
- Trim `service-data-browsing`'s "No resource is a pass-through to an arbitrary path" and "Stable,
  branchable error ids" requirements to reference the platform-wide rule instead of restating it,
  keeping only the detail genuinely specific to that capability (its own catalogue mechanism, its
  own concrete error id list).
- Trim `api-catalog-browser`'s CORS-limitation requirement so the "no pass-through proxy exists"
  clause references the platform rule instead of restating it, keeping the Swagger-page-specific
  CORS disclosure behavior intact.
- Remove the now-redundant "frontend talks only to this backend" and "prefer a service's own client
  library" bullets from `docs/Purpose.md`'s Design decisions, replaced with a pointer to the new
  capability.

## Capabilities

### New Capabilities
- `platform-conventions`: cross-feature rules that apply to more than one capability - no
  arbitrary pass-through, preferring client libraries, stable error ids.

### Modified Capabilities
- `service-data-browsing`: its pass-through and stable-error-id requirements now reference
  `platform-conventions` for the general rule, keeping only their own capability-specific detail.
- `api-catalog-browser`: its CORS-limitation requirement now references `platform-conventions` for
  the no-pass-through-proxy fact instead of restating it.

## Impact

Only documentation (`openspec/specs/**`, `docs/Purpose.md`) changes; no application code changes.
