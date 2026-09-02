# Purpose

An internal, developer-only tool. Real customers never reach it. It exists to make data and
configuration that the main Numo UI hides - or never surfaces at all - browsable during
development and support work.

Because the audience is developers, the usual product pressures do not apply: no i18n, no
polished empty states, no per-customer branding. Legibility and low effort to add the next
service win over anything else.

## Planned features

1. **Swagger UI for multiple services.** One place that renders any Numo service's OpenAPI
   spec, with a picker to switch between services, instead of hunting for each service's own
   `/swagger` URL.
2. **LaunchDarkly feature-toggle browser.** List the current toggles and their values, and copy
   a selection as JSON shaped for pasting into a local `appsettings.Development.json`. The
   copy-out format is the point of the feature, not a nicety.
3. **Service data browsing.** Lists of records, and a single-record view for a chosen row.

None of these are built yet. `Features/SampleItems/` is scaffolding that proves the pipeline
end to end; it is not one of them.

## Design decisions

- **The frontend talks only to this backend.** Never call another Numo service from the browser.
  A developer machine cannot necessarily reach those services, but DevServices is deployed
  alongside them inside the network, so it can. Every upstream call belongs server-side.
- **Prefer a service's own client library.** If a service ships one - `Numo.Employee.Client.Lib`
  and its siblings - consume it instead of hand-rolling an HTTP client against its API.
- **Keep both ends lean, and drive the UI from data.** The frontend should render generic
  components against the JSON structure the backend returns, rather than growing a bespoke typed
  page per service and per record type. Expect exceptions where a feature genuinely needs its own
  shape - the toggle copy-out and the Swagger host both do - but a new list of records should
  ideally be a new backend endpoint and no new frontend code.

## Open questions

- **Does this app need its own database?** The scaffold ships EF Core, Postgres, migrations, and
  the `DevServices` schema, but all three planned features read from elsewhere: OpenAPI specs,
  the LaunchDarkly API, other services' client libs. Until something needs to be persisted here
  (saved queries, a service registry, cached specs), the persistence layer is unused weight.
  Decide before building feature work on top of it.
- **How is the service list configured?** Features 1 and 3 both need to know which services
  exist and where they are. Configuration, discovery, or hardcoded per environment is unsettled.
- **What does "generic components from JSON" mean concretely?** A column-inferring table over
  arbitrary JSON is very different from a backend that returns an explicit display descriptor
  (columns, labels, formats) alongside the rows. The second keeps the frontend dumber and is
  worth considering first.
