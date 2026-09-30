# platform-conventions Specification

## Purpose
Rules that apply across more than one capability, so they have exactly one home instead of being
restated (and risking drift) in each feature that follows them: no endpoint is ever a
pass-through to an arbitrary path, a service's own client library is preferred over a hand-rolled
`HttpClient`, and every slice's failures carry stable, branchable error ids. A capability that
implements one of these documents its own concrete instance or exception in its own spec, and
references this one for the general rule.
## Requirements
### Requirement: No endpoint is a pass-through to an arbitrary path
Every endpoint that reaches another Numo service SHALL resolve through a narrow, fixed mapping - a
resource key, service name, or similar identifier in a closed, compile-time-registered catalogue -
never a caller-supplied path forwarded verbatim to an arbitrary location on that service. This
applies to every feature that talks to another service, not just one of them: a general
pass-through route would become a hole into the internal network if this app were ever exposed
outside it. `service-data-browsing` and `api-catalog-browser` each document their own concrete
instance of this rule (a resource catalogue and a fixed OpenAPI-document path, respectively).

#### Scenario: A caller cannot reach an arbitrary path on a configured service
- **WHEN** a request names an identifier that does not resolve through the feature's own closed
  catalogue of known services or resources
- **THEN** the request is rejected before any downstream call is attempted, rather than being
  forwarded to whatever path the caller supplied

### Requirement: Prefer a service's own client library
When a Numo service ships its own client library, a feature that reaches that service SHALL
consume the library rather than hand-rolling an `HttpClient` against the service's API directly. A
hand-rolled `HttpClient` is only acceptable when no client library exists, or when the library's
own methods do not cover the needed route; each such exception SHALL be documented, in the
resource or feature that needs it, with the specific reason no library method fit.

#### Scenario: A service with a client library is reached through it
- **WHEN** a feature needs data from a service that ships `Numo.<Service>.Lib` (or similarly
  named)
- **THEN** the feature consumes that library's client interface rather than constructing its own
  `HttpClient` against the service's routes

#### Scenario: A hand-rolled HttpClient is the documented exception
- **WHEN** no client library exists for a service, or the library has no method for the needed
  route (as `service-data-browsing` documents for every DataIntegration resource and for
  `department-roles`)
- **THEN** the feature may use its own `HttpClient`, with the specific gap in the library
  documented in the code, not silently substituted without explanation

### Requirement: Failures carry stable, branchable error ids
Every failure a feature's backend slice can produce SHALL carry a stable `NumoError` id, declared
once per slice, so a caller can branch on the id rather than parsing the failure message. Each
slice documents its own concrete set of ids as part of its own capability spec; this requirement is
the platform-wide expectation that such a set exists and stays stable once published.

#### Scenario: A caller branches on an error id, not a message
- **WHEN** a slice's handler fails for a reason the caller needs to distinguish from other
  failures (a missing record, a validation failure, an unreachable downstream service, and so on)
- **THEN** the failure carries one of that slice's own stable, named error ids, and the id - not
  the human-readable message - is what a caller branches on

