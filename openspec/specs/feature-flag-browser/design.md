# feature-flag-browser design

## How it works
The backend reads the project's environments and then its flags from the LaunchDarkly REST API, and
returns one row per flag with its state in each environment. The page renders a grid with one
column per environment and filters by tag and by "differs across environments", with environments
that can be hidden.

## Decisions
- **REST API with an access token, not the server SDK.** The SDK only evaluates a flag already known
  by name, and its all-flags state carries keys and values but never names, descriptions or tags:
  LaunchDarkly does not send project metadata to SDKs at all. The REST API needs an API access
  token rather than the SDK key. It is bound under the same LaunchDarkly configuration section as
  the SDK settings, so every LaunchDarkly credential lives in one place.
- **Environments are read first, then flags.** The flags endpoint leaves out per-environment state
  unless every wanted environment is named in a repeated `env` parameter. The default summary
  representation is enough: it marks the fallthrough and off variations, which is all the served
  value needs, while `summary=0` would add every targeting rule the page never shows.
- **The served value is the primary accent; targeting on/off is a secondary note.** A developer
  scanning the grid wants to know what a flag serves now. True and false get fixed colors, a
  multivariate value gets a per-row categorical color, a rollout or SDK default gets a neutral tag,
  and "Targeting off" is a small line under the cell.
- **Multivariate colors are assigned per flag row, in first-seen order, over visible environments
  only.** Two unrelated flags' values never share a color by coincidence, since a color means
  "distinct within this row". A hidden column never uses up a color, so colors stay stable as
  environments are toggled. Values beyond the fixed palette share one neutral "Other" color rather
  than reusing a hue that would imply a false match.
- **The picker stores hidden environments, not visible ones.** An environment LaunchDarkly adds
  later then shows up for every reader automatically, instead of silently missing from a stored
  allow-list. Storage failures (private browsing, blocked site data) fall back to hiding nothing for
  the session.
- **"Differs across environments" compares served values, not targeting state.** Two environments
  serving `true` are not different just because one reaches it with targeting off. The filter is
  disabled with fewer than two visible environments, where "differs" means nothing.
- **The tag filter is OR across selected tags, with "No tag" as one more option in that OR.** Its
  sentinel key cannot collide with a real tag, which is identifier-shaped. An empty selection shows
  every flag, the way an unchecked multiselect reads.

## Approaches that don't work
- **Accenting whether targeting is on.** It makes the mechanism compete with the value for
  attention, and a developer reading the grid needs the value.

## Constraints and limitations
- The flag list stops after a fixed number of pages, with a logged warning. No project in use comes
  close to that limit.
- Hidden environments are stored per browser, so they don't follow a developer across machines.
- The JSON copy-out for a local `appsettings.Development.json` is planned but not built.
