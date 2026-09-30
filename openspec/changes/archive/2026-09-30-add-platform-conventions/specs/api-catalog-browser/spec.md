## MODIFIED Requirements

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
