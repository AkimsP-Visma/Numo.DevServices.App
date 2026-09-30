## MODIFIED Requirements

### Requirement: Stable, branchable error ids
Every failure this slice can produce SHALL carry a stable `NumoError` id - `UnknownResource`,
`TenantIdMissing`, `RecordNotFound`, `DownstreamCallFailed`, `DownstreamCallUnauthorized`, or
`RequiredFilterMissing` - so a caller can branch on the id rather than parsing the message, per the
platform-wide convention recorded in `platform-conventions`. `DownstreamCallUnauthorized` SHALL be
distinguished from `DownstreamCallFailed` because the client libraries throw client-side, before
any request is sent, when the app lacks an authenticated principal and
`FeatureManagement:AllowUnauthorizedApiCalls` is not set - there is no HTTP status to report in
that case.

#### Scenario: A missing record yields RecordNotFound, not a 500
- **WHEN** a detail request names a valid resource and a well-formed id the downstream service has
  no record for
- **THEN** the response carries the `RecordNotFound` error id, not an unhandled exception

#### Scenario: A client-side authorization rejection is distinguished from a downstream failure
- **WHEN** a client library throws `UnauthorizedException` before sending a request
- **THEN** the response carries `DownstreamCallUnauthorized`, distinct from `DownstreamCallFailed`,
  and carries no status code

### Requirement: No resource is a pass-through to an arbitrary path
Each resource SHALL be bound to a fixed path (or fixed set of paths) on a service named by a
configuration key, resolved through a closed, compile-time-registered catalogue - `{resource}` in
the route is a key into that catalogue, never a caller-supplied path forwarded verbatim. This is
this slice's own instance of the platform-wide no-pass-through rule recorded in
`platform-conventions`.

#### Scenario: An unregistered resource key never reaches a downstream service
- **WHEN** a request names a resource key with no matching registered `IServiceDataResource`
- **THEN** no downstream HTTP call is made at all; the handler fails with `UnknownResource`
