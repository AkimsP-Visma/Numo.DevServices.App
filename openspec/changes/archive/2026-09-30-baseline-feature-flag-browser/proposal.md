## Why

The LaunchDarkly feature-toggle browser (`Features/FeatureFlags/` plus the `/feature-flags` page)
is already built and has since been extended with several UX refinements - per-environment value
rendering, an environment show/hide picker, a differs-across-environments filter, and a tag filter.
None of this is captured as an OpenSpec capability yet. This change retroactively documents the
feature exactly as it exists in code today, so OpenSpec has a baseline spec to diff future changes
against. It proposes no new behavior and changes no code.

## What Changes

- Document the existing `feature-flag-browser` capability: listing LaunchDarkly flags with
  per-environment state, sourced from the LaunchDarkly REST API (not the SDK).
- Capture the value-over-targeting-state rendering rules (boolean coloring, per-row multivariate
  coloring with an "Other" fallback, rollout and SDK-default states, targeting-off as a secondary
  note).
- Capture the environment picker and its hidden-set localStorage persistence.
- Capture the "only show flags that differ across visible environments" filter, its comparison
  rule, and its two-environment minimum.
- Capture the tag OR filter, including the synthetic "No tag" option and the empty-selection-means-
  unfiltered rule.

## Capabilities

### New Capabilities
- `feature-flag-browser`: Lists a LaunchDarkly project's feature flags with per-environment state,
  and lets a developer filter and visually compare that state across environments and tags.

### Modified Capabilities
(none)

## Impact

- Backend: `src/Numo.DevServices.Api/Features/FeatureFlags/` (no changes; documented as-is).
- Frontend: `src/Numo.DevServices.Api/ClientApp/src/app/features/feature-flags/` (no changes;
  documented as-is).
- No code, tests, or configuration are modified by this change - only `openspec/` artifacts are
  added.
