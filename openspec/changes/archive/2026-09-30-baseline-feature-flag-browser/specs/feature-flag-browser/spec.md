## ADDED Requirements

### Requirement: List flags with per-environment state
The system SHALL list every non-archived feature flag of the configured LaunchDarkly project,
ordered by key, together with its served state (on/off, served value or rollout, is-rollout) in
each of the project's environments. The list of environment keys SHALL come from the project's
own environments, in the order LaunchDarkly returns them, so the frontend needs no separate
configuration of which environments exist.

#### Scenario: Flags load with per-environment state
- **WHEN** the feature-flags page loads
- **THEN** it displays one row per non-archived flag, one column per project environment, and
  each cell shows that flag's served state in that environment

#### Scenario: LaunchDarkly is not configured
- **WHEN** the LaunchDarkly API token or project key is missing from configuration
- **THEN** the request fails with a stable "not configured" error instead of attempting a call

#### Scenario: LaunchDarkly API call fails
- **WHEN** a LaunchDarkly API request returns a non-success status, an empty document, or throws
  a network/timeout/JSON error
- **THEN** the request fails with a stable "LaunchDarkly unavailable" error that includes the
  reason, and the page shows that error instead of a partial table

### Requirement: Value is the primary accent, targeting state is a secondary note
The system SHALL render each environment cell's primary visual accent from the flag's served
value, not from whether targeting is on for that environment. Targeting-off SHALL be shown only
as a small secondary note alongside the value.

#### Scenario: Boolean value coloring
- **WHEN** an environment's served value is the boolean `true` or `false`
- **THEN** the cell shows a "True" tag colored as success or a "False" tag colored as danger,
  regardless of whether targeting is on or off

#### Scenario: Targeting off is a secondary note
- **WHEN** a flag's targeting is off in an environment
- **THEN** the cell still renders primarily by its served value (the off-variation's value), and
  additionally shows a small "Targeting off" note below the value

#### Scenario: Percentage rollout
- **WHEN** an environment's fallthrough serves more than one variation (a percentage rollout)
- **THEN** the cell shows a neutral "percentage rollout" label instead of any single value

#### Scenario: No variation marked (SDK default)
- **WHEN** a flag has no variation marked as served for its current on/off state in an environment
- **THEN** the cell shows a neutral "SDK default" label, since the actual value depends on the
  calling SDK's own default and is unknowable here

### Requirement: Multivariate values get a per-row, fixed-order categorical color
For a flag whose served value is neither boolean, rollout, nor SDK-default, the system SHALL
assign each distinct value a color from a fixed categorical palette, scoped to that flag's own
row and to the currently visible environment columns only.

#### Scenario: Distinct values get distinct colors within one flag's row
- **WHEN** a flag serves two or more distinct non-boolean string/number values across its visible
  environments
- **THEN** each distinct value is assigned a different color from the fixed palette, in the order
  the values are first encountered across the visible environment columns

#### Scenario: More distinct values than palette colors
- **WHEN** a flag has more than 8 distinct non-boolean values across its visible environments
- **THEN** the 9th and subsequent distinct values all render with a shared neutral "Other" color
  instead of reusing or cycling an already-assigned hue

#### Scenario: Color assignment ignores hidden environments and other flags
- **WHEN** an environment column is hidden by the environment picker, or when a different flag
  happens to serve a value that looks the same
- **THEN** the hidden environment's value does not consume a color slot, and the other flag's
  color assignment is entirely independent (colors are not shared or implied across flags)

### Requirement: Environment show/hide picker persisted as a hidden set
The system SHALL let the reader hide or show each project environment as a table column, via a
checkbox per environment, and SHALL persist the choice in the browser's localStorage as the set
of hidden environment keys (not the set of visible ones).

#### Scenario: Hiding an environment removes its column
- **WHEN** the reader unchecks an environment in the picker
- **THEN** that environment's column is removed from the table, and the picker itself still
  lists it so it can be shown again

#### Scenario: Choice persists across reloads
- **WHEN** the reader hides an environment and reloads the page
- **THEN** that environment remains hidden, because its key is read back out of localStorage

#### Scenario: A newly added environment is visible by default
- **WHEN** the LaunchDarkly project gains a new environment that the reader's stored hidden set
  has never seen
- **THEN** that environment's column shows by default, since only explicitly hidden keys are
  ever suppressed

#### Scenario: Storage is unavailable
- **WHEN** localStorage read or write throws (for example, private browsing or blocked site data)
- **THEN** the picker still works for the current page load using an in-memory hidden set, and
  only the persistence across reloads is lost

### Requirement: Filter to flags that differ across visible environments
The system SHALL offer a checkbox that, when enabled, shows only flags whose canonical state
(value, rollout-ness, or SDK-default-ness) differs across the currently visible environments,
ignoring whether targeting is on or off. This checkbox SHALL be disabled whenever fewer than two
environments are visible.

#### Scenario: Filter hides identical flags
- **WHEN** the "only show flags that differ across visible environments" checkbox is checked
- **THEN** a flag whose served value, rollout-ness and SDK-default-ness are identical across
  every visible environment is excluded from the table

#### Scenario: Targeting state alone does not count as a difference
- **WHEN** a flag serves the same value in every visible environment but has targeting on in one
  and off in another
- **THEN** that flag is still treated as non-differing and is excluded by the filter

#### Scenario: Filter disabled below two visible environments
- **WHEN** fewer than two environments are currently visible
- **THEN** the checkbox is disabled (and, if it was checked, its effect is not applied), since
  there is nothing to compare against

#### Scenario: Comparison follows the visible set, not the full environment list
- **WHEN** an environment is hidden by the environment picker
- **THEN** its state is excluded from the differs comparison, even if it would have introduced a
  difference

### Requirement: Tag OR filter with a "No tag" option
The system SHALL offer a checkbox multiselect of every distinct tag used by any flag, plus a
synthetic "No tag" option, filtering flags by OR logic across the selected entries. An empty
selection SHALL show every flag (unfiltered), not none.

#### Scenario: Selecting one tag shows flags carrying it
- **WHEN** the reader checks a single real tag
- **THEN** only flags that carry that tag remain visible

#### Scenario: Selecting multiple tags is OR, not AND
- **WHEN** the reader checks two or more real tags
- **THEN** a flag matching any one of the selected tags remains visible, even if it lacks the
  others

#### Scenario: "No tag" is an ordinary option in the same OR
- **WHEN** the reader checks "No tag" alongside one or more real tags
- **THEN** flags with zero tags and flags matching any selected real tag both remain visible, as
  a single combined OR

#### Scenario: Empty selection is unfiltered
- **WHEN** no tag checkbox (including "No tag") is selected
- **THEN** every flag is shown, regardless of its tags

#### Scenario: Tag options are not affected by other active filters
- **WHEN** the environment picker or differs-filter narrows which flags or columns are shown
- **THEN** the tag checkbox list still offers every tag that exists on any flag, so unchecking a
  tag never makes its own checkbox disappear
