# api-catalog-browser Specification

## Purpose
Lets a developer browse any configured Numo service's OpenAPI spec from one page - a service
picker plus a rendered Swagger UI - instead of hunting down each service's own `/swagger` URL.
Backed by a narrow `GET api/services/{name}/openapi` endpoint that rewrites the document's
`servers` entry to the service's active-environment location; no general pass-through proxy exists.
## Requirements
### Requirement: Service list endpoint
The system SHALL expose `GET api/services`, returning the Numo services registered for the
currently active environment, each with its name and location, ordered by name. A service whose
configured `Location` is blank or whitespace for the current environment SHALL be omitted from the
result rather than returned with an empty location.

#### Scenario: Listing services for the active environment
- **WHEN** a client calls `GET api/services`
- **THEN** the response is a list of `{ name, location }` entries, one per configured service that
  has a non-blank `Location` in the currently selected environment, sorted alphabetically by name

#### Scenario: A configured service has no location
- **WHEN** an environment's `Services` section contains an entry whose `Location` is missing or
  blank
- **THEN** that entry is excluded from the `GET api/services` response and a warning is logged,
  rather than the entry being returned or the request failing

### Requirement: Service list reflects the active environment
The system SHALL resolve the service list through `IServiceDiscoveryService`, which reads whichever
environment (Testing, Staging, Production, or Local) is currently selected, so that switching the
active environment changes the services and locations offered on the very next request, with no
caching of the previous environment's list.

#### Scenario: Environment switch changes the offered services
- **WHEN** the active environment is changed (via the Environments feature) between one call to
  `GET api/services` and the next
- **THEN** the second call's response reflects the newly active environment's `Services` definitions,
  not the previous environment's

### Requirement: Service OpenAPI document endpoint
The system SHALL expose `GET api/services/{serviceName}/openapi`, which fetches the named service's
OpenAPI document server-side from a fixed path on that service and returns it as JSON. The endpoint
SHALL only resolve `serviceName` against the service names known to `IServiceDiscoveryService` for
the current environment; it SHALL NOT accept or forward any path other than the fixed document path,
so it cannot be used to reach an arbitrary path on the service.

#### Scenario: Fetching a known service's OpenAPI document
- **WHEN** a client calls `GET api/services/{serviceName}/openapi` for a service name that is
  registered with a location in the current environment
- **THEN** the backend requests `swagger/v1/swagger.json` at that service's configured location and
  returns the parsed document on success

#### Scenario: Unknown service name
- **WHEN** a client calls `GET api/services/{serviceName}/openapi` for a name not registered in the
  current environment (or with no location configured)
- **THEN** the request fails with a stable `UnknownService` error identifying the service name,
  rather than attempting an HTTP call

#### Scenario: Service unreachable or returns a non-success status
- **WHEN** the underlying request to the service's `swagger/v1/swagger.json` path throws
  (`HttpRequestException`, `JsonException`, or `TaskCanceledException`) or returns a non-success HTTP
  status code
- **THEN** the request fails with a stable `OpenApiUnavailable` error naming the service and the
  reason (the status code or exception message), and the failure is logged as a warning

#### Scenario: Service returns an empty or unparsable body
- **WHEN** the service responds successfully but the body does not parse to a JSON document
- **THEN** the request fails with a stable `OpenApiUnavailable` error rather than returning an empty
  or null result

### Requirement: Served document points "Try it out" at the real service
Before returning a fetched OpenAPI document, the system SHALL prepend an entry to the document's
`servers` array containing the service's configured absolute location (with the description
"Configured location, called straight from the browser"), so that a client rendering the document
targets the real service rather than resolving the document's own relative server entries against
this app's origin. If the document has no `servers` array, the system SHALL leave the document
unchanged.

#### Scenario: Document has a servers array
- **WHEN** a fetched OpenAPI document contains a `servers` array (typically relative entries such as
  `/employee-api` or `/`)
- **THEN** the returned document has the service's configured absolute location inserted as the
  first entry of that array, ahead of the service's own declared servers

#### Scenario: Document has no servers array
- **WHEN** a fetched OpenAPI document has no `servers` property
- **THEN** the document is returned unchanged, with no `servers` property added

### Requirement: Swagger page with a service picker
The frontend SHALL provide a page (routed at `/swagger`, and reachable as the app's default landing
route) that lists the available services in a picker and, once a service is selected, renders that
service's OpenAPI document using Swagger UI, fetched from this app's own OpenAPI endpoint rather than
from the service directly.

#### Scenario: Page loads the service list
- **WHEN** the Swagger page initializes
- **THEN** it calls `GET api/services` and populates the picker with the returned services; if that
  call fails, it shows an error message instead of the picker's options

#### Scenario: Selecting a service renders its documentation
- **WHEN** a developer selects a service from the picker
- **THEN** the page loads the Swagger UI bundle and stylesheet on demand (if not already loaded),
  clears any previously rendered service's UI, and renders Swagger UI against this app's
  `api/services/{serviceName}/openapi` URL, showing a loading indicator until Swagger UI reports
  completion

#### Scenario: Selected service's document cannot be loaded
- **WHEN** Swagger UI reports a failure loading the document for the selected service (including when
  `GET api/services/{serviceName}/openapi` itself failed)
- **THEN** the page stops showing the loading indicator and displays an error message naming the
  service, instead of leaving Swagger UI in a loading state

#### Scenario: No service selected yet
- **WHEN** no service has been selected and no error has occurred
- **THEN** the page shows a hint prompting the developer to select a service, instead of an empty
  Swagger UI container

### Requirement: "Try it out" CORS limitation is disclosed and only fully worked around in local development
The Swagger page SHALL disclose, directly on the page, that using Swagger UI's "Try it out" sends a
request straight from the browser to the service's real location (per the prepended server), and
that this fails under normal browsing because Numo services send no CORS headers and answer
`OPTIONS` preflights with a non-success status. Per the platform-wide no-pass-through rule recorded
in `platform-conventions`, the system does not provide a pass-through proxy route to work around
this in the deployed app; the local development server MAY additionally be configured to proxy a
service's prefix so requests become same-origin during `ng serve`.

#### Scenario: Notice is always visible
- **WHEN** the Swagger page is rendered, regardless of whether a service is selected
- **THEN** a notice is shown stating that "Try it out" calls the service directly and will fail
  without CORS being disabled (e.g. via a browser extension)

#### Scenario: No generic proxy exists to route around CORS
- **WHEN** a developer wants "Try it out" to work against a service without a CORS-disabling
  browser extension
- **THEN** no backend route exists that forwards an arbitrary path to an arbitrary configured
  service on the developer's behalf, consistent with `platform-conventions`; only the fixed-path
  OpenAPI document endpoint exists

