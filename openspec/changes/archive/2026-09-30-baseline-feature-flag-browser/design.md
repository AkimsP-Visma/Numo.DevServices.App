## Context

The feature-toggle browser is already implemented (`Features/FeatureFlags/` backend,
`ClientApp/src/app/features/feature-flags/` frontend). This design document records the real
architectural decisions already made in that code, as a baseline for future changes to diff
against - it proposes nothing new.

## Goals / Non-Goals

**Goals:**
- Record why the backend reads the LaunchDarkly REST API instead of the server SDK.
- Record why the page renders a flag's served value as the primary accent instead of whether
  targeting is on.
- Record why the environment picker persists a hidden set rather than a visible set.
- Record why a flag's multivariate colors are assigned in a fixed, non-cycled order, scoped per
  flag row rather than globally.

**Non-Goals:**
- Changing any of the above. This document describes shipped behavior only.
- The toggle copy-out feature mentioned in `docs/Purpose.md` (Feature 2's second half) - not
  built, out of scope for this baseline.

## Decisions

**REST API with an access token, not the server SDK.** `IFeatureFlagService` from
`Numo.Common.Lib` only answers `IsEnabledAsync(name)` for a flag already known by name, and the
underlying server SDK's `AllFlagsState` returns keys and values but never names, descriptions or
tags - LaunchDarkly does not send that project metadata to SDKs at all. Listing flags for a
browsing UI therefore has to come from `GET /api/v2/flags/{projectKey}`, which authenticates with
an API access token (`LaunchDarklyApiOptions.ApiToken`) rather than an SDK key. This is a second,
independent set of LaunchDarkly credentials from `Numo.Common.Lib`'s own options, deliberately
bound under the same `FeatureFlagOptions:LaunchDarkly` configuration section so every LaunchDarkly
credential still lives in one place.

**Two calls to get per-environment state: environments, then flags.** The flags endpoint omits
the `environments` object entirely unless every wanted environment is named in a repeated `env`
query parameter, so `GetFeatureFlagsHandler` reads the project's environments first
(`GET /api/v2/projects/{projectKey}/environments`) and threads their keys into the flags request.
The default summary representation (not `summary=0`) is requested: `_summary` marks which
variation index is the fallthrough and which is the off one, which is all the served value needs;
`summary=0` would additionally return every targeting rule, which the page never shows.

**Value is the primary accent; targeting on/off is a secondary note.** Earlier iterations of this
kind of page tend to accent whether targeting is on. This implementation instead colors the cell
by the actual served value - true/false get fixed severities, a multivariate value gets a
per-flag categorical color, a rollout or SDK-default gets a neutral tag - and demotes "Targeting
off" to a small line under the cell. Rationale: a developer scanning the grid wants to know what
a flag currently serves, not the on/off mechanism that produced it; on/off is still visible, just
not competing with the value for visual weight.

**Multivariate colors are fixed-order per flag row, not cycled, not global.** `discreteValueColor`
walks only the currently *visible* environments of one flag, collects each value's label in first-
seen order, and assigns colors from a fixed 8-entry palette (`VALUE_COLORS`) in that order; a 9th
or later distinct value falls back to a shared neutral "Other" color instead of reusing a hue.
Scoping is deliberately per flag row: two unrelated flags' "variation 0" values do not share a
color by coincidence, since the color encodes "distinct within this row," not a global variation
index. Scoping to visible (not all declared) environments means a hidden column's value never
consumes a color slot nobody sees, keeping the colors stable as the reader toggles environments.

**The environment picker persists a hidden set, not a visible set.** `HiddenEnvironmentsStore`
stores which environment keys the reader has explicitly hidden, keyed in localStorage as
`devservices.feature-flags.hidden-environments`. Storing the complement (hidden, not visible)
means an environment LaunchDarkly adds later shows up automatically for every reader, because it
is absent from everyone's stored hidden set, rather than silently missing from a stored
allow-list until each reader notices and opts back in. Read/write failures (private browsing,
blocked storage) degrade to an empty hidden set and a swallowed write - the in-memory signal still
works for the session, only persistence is lost.

**The differs-across-environments filter compares canonical value, not targeting state.**
`hasDifferingValues` builds a per-environment canonical marker (the JSON value, or a sentinel for
rollout / SDK-default / absent) and compares only those markers across the *visible* environment
set, ignoring `isOn`. This matches the same "value is what matters" framing as the coloring
decision above: two environments that both serve `true` are not "different" just because one has
targeting on and the other has it in an always-true fallthrough. The filter is disabled below two
visible environments (`MIN_ENVIRONMENTS_TO_COMPARE`), since "differs" has no meaning with zero or
one column to compare.

**The tag filter is OR across selections, with "No tag" as an ordinary option in the same OR, and
empty selection means unfiltered.** `NO_TAG_KEY` is a sentinel (`\u0000no-tag`) that cannot
collide with a real LaunchDarkly tag, which is identifier-shaped. Checking it is not a separate
mode - `matchesTagFilter` just tests "this flag has zero tags" as one more condition ORed with the
selected real tags. An empty selection set shows every flag, matching how a checkbox multiselect
with nothing checked reads to a user (no active constraint), not "show nothing."

## Risks / Trade-offs

- [Risk] The 8-color fixed palette can still run out on a flag with many multivariate values →
  Mitigation: the 9th+ distinct value folds into a shared "Other" gray rather than erroring or
  reusing a hue that would imply a false match.
- [Risk] `MaxFlagPageCount` (50 pages of 100) silently truncates an unusually large project's flag
  list rather than failing → Mitigation: a warning is logged when the cap is hit; accepted as a
  soft limit rather than an error since no project observed in practice approaches it.
- [Risk] LocalStorage persistence for hidden environments is per-browser, not shared across a
  developer's machines → Mitigation: accepted as inherent to browser storage; the empty-hidden-set
  fallback means a new browser simply shows every environment by default.

## Migration Plan

Not applicable - this change adds only `openspec/` documentation artifacts and modifies no code,
so there is nothing to deploy or roll back.

## Open Questions

None - every decision above is already settled in the shipped code.
