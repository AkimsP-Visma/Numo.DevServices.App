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
   `/swagger` URL. *Built* - `Features/Services/` plus the `/swagger` page.
2. **LaunchDarkly feature-toggle browser.** List the current toggles and their values, and copy
   a selection as JSON shaped for pasting into a local `appsettings.Development.json`. The
   copy-out format is the point of the feature, not a nicety.
3. **Service data browsing.** Lists of records, and a single-record view for a chosen row.

Feature 1 is built; 2 and 3 are not. `Features/SampleItems/` is scaffolding that proves the
pipeline end to end; it is not one of them.

## Design decisions

- **The frontend talks only to this backend, and this backend is not a reverse proxy.** Avoid
  calling another Numo service from the browser: a developer machine cannot necessarily reach those
  services, while DevServices is deployed alongside them inside the network. But the way to do that
  is a narrow endpoint per need, each reaching a fixed path on a service named by a configuration
  key - never a pass-through route that forwards an arbitrary path, which would become a hole into
  the internal network if this app were ever exposed outside it.
- **Consequence for Swagger UI: "Try it out" reaches the service but the browser blocks the
  response.** Numo specs declare relative servers (`/employee-api`, `/`), which resolve against this
  app's own origin, so the served document gets the service's configured location prepended as the
  first server. Requests then go to the service directly - and the services send no
  `Access-Control-Allow-Origin` and answer `OPTIONS` with 405, so CORS stops them. Two ways round it
  locally: a CORS-disabling browser extension, which is what we do today and is accepted for a
  developer-only tool, or proxying the service prefix in `proxy.conf.js` so everything is
  same-origin - dev-server-only either way, adding nothing to the deployed app. Making Execute work
  in the deployed app would need the pass-through route above, which is why it does not work there.
- **Prefer a service's own client library.** If a service ships one - `Numo.Employee.Client.Lib`
  and its siblings - consume it instead of hand-rolling an HTTP client against its API.
- **Keep both ends lean, and drive the UI from data.** The frontend should render generic
  components against the JSON structure the backend returns, rather than growing a bespoke typed
  page per service and per record type. Expect exceptions where a feature genuinely needs its own
  shape - the toggle copy-out and the Swagger host both do - but a new list of records should
  ideally be a new backend endpoint and no new frontend code.

## Open questions

- **When does the service registry move into the database?** The list currently comes from the
  `Services` section of `appsettings.Development.json` via `IServiceDiscoveryService`. The intended
  next step is storing it in this app's database and editing it through the UI, which is what the
  scaffolded EF Core setup is for. The swap is a second implementation of that same interface plus
  a registration change - handlers do not know where the list came from.
- **`Numo.Common.Lib` also brings `IFeatureFlagService` and Microsoft.FeatureManagement**, already
  registered by `AddNumoCommonServices`. Worth looking at first when starting the LaunchDarkly
  feature rather than adding another SDK.
- **What does "generic components from JSON" mean concretely?** A column-inferring table over
  arbitrary JSON is very different from a backend that returns an explicit display descriptor
  (columns, labels, formats) alongside the rows. The second keeps the frontend dumber and is
  worth considering first.
