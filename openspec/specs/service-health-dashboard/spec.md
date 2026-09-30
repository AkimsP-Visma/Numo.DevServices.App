# service-health-dashboard Specification

## Purpose
Shows whether every service configured for the active environment answers its
`api/platform/microservice/ping` endpoint with `pong`, refreshing automatically while the page is
open. One service being down is data in the row set, not a failure of the check itself - the
endpoint always answers 200 with a row per service.
## Requirements
### Requirement: List every configured service for the active environment
The system SHALL determine the set of services to check from the currently active environment's
service-location configuration (`Features/Environments/`), and SHALL exclude any configured service
whose location is missing or blank from the check.

#### Scenario: A service has no configured location
- **WHEN** the active environment defines a service entry with a blank or missing `Location`
- **THEN** that service is excluded from the ping round and is not pinged
- **AND** the omission is logged as a warning identifying the service by name

#### Scenario: The active environment is switched
- **WHEN** the active environment is changed (for example from Testing to Staging)
- **THEN** the very next service-health request checks the new environment's configured services
  instead of the previous environment's, with no caching of the prior set

### Requirement: Ping every configured service and report per-service outcome
The system SHALL send an HTTP GET to `api/platform/microservice/ping` under each configured
service's location, and SHALL report, for every configured service with a usable location, whether
it is up, its HTTP status code (when one was received), a response time in milliseconds, and - when
down - a human-readable reason.

#### Scenario: A configured location is not a usable absolute URL
- **WHEN** a service's configured location cannot be combined with the ping path into a valid
  absolute URL
- **THEN** that service is reported as down with a reason describing the invalid location, and no
  HTTP request is attempted for it

#### Scenario: All configured services are pinged concurrently
- **WHEN** a service-health check request is received
- **THEN** every configured service (with a usable location) is pinged concurrently
- **AND** the response lists one entry per pinged service

### Requirement: Determine up/down from status code and response body together
The system SHALL consider a service up only when its ping response has HTTP status code 200 and a
response body that, once trimmed, is equal to `pong` case-insensitively. Any other outcome - a
non-200 status, a 200 with a different body, a transport-level failure, or a timeout - SHALL be
reported as down, never as a request-level failure of the health check itself.

#### Scenario: Ping answers 200 with the expected body
- **WHEN** a service's ping endpoint answers HTTP 200 with a body that trims to `pong`
  (case-insensitive)
- **THEN** the service is reported as up, with its status code and response time

#### Scenario: Ping answers 200 with an unexpected body
- **WHEN** a service's ping endpoint answers HTTP 200 with a body that is not `pong` once trimmed
- **THEN** the service is reported as down
- **AND** the down reason includes a shortened preview of the unexpected body, not the full body

#### Scenario: Ping answers a non-200 status
- **WHEN** a service's ping endpoint answers any status code other than 200
- **THEN** the service is reported as down with a reason naming the status code received

#### Scenario: Ping fails at the transport level or times out
- **WHEN** the HTTP request to a service's ping endpoint fails (connection error) or exceeds the
  configured request timeout
- **THEN** the service is reported as down with a reason describing the failure
- **AND** no status code is reported for that service

### Requirement: One service being down never fails the overall health check
The system SHALL always return a successful result containing one row per configured, reachable-
location service, regardless of how many of those services are individually reported as down. The
health-check request SHALL only fail when the service configuration itself cannot be read.

#### Scenario: One of several configured services is down
- **WHEN** one configured service's ping is down while the others are up
- **THEN** the overall service-health request still succeeds
- **AND** the result includes a row for the down service alongside rows for the up services

#### Scenario: Every configured service is down
- **WHEN** every configured service's ping is down
- **THEN** the overall service-health request still succeeds
- **AND** the result includes a down row for every one of them

### Requirement: Dashboard page renders one status row per service and refreshes while open
The system SHALL present the ping results as a grid with one card per service, showing the service
name, its configured location, an up/down indicator, and either its response time (when up) or its
down reason (when down). While the page remains open, the system SHALL automatically re-run the
check on a fixed interval, and SHALL also allow a manual, on-demand re-check.

#### Scenario: Page loads and displays results
- **WHEN** the service-health page is opened
- **THEN** it immediately requests a status snapshot and renders a card per returned service,
  each tagged "Up" or "Down" accordingly

#### Scenario: Page auto-refreshes while open
- **WHEN** the service-health page has been open for the duration of one refresh interval
- **THEN** it automatically requests a new status snapshot and updates the displayed cards
- **AND** it stops refreshing once the page is navigated away from

#### Scenario: A refresh is already in flight when the next interval tick occurs
- **WHEN** the automatic refresh interval elapses while a previous status request is still pending
- **THEN** the pending tick is skipped rather than starting a second concurrent request

#### Scenario: Manual refresh
- **WHEN** the user activates the manual "Refresh" control
- **THEN** the page immediately requests a new status snapshot outside of the regular interval

#### Scenario: No services are configured
- **WHEN** the active environment has no services with a usable configured location
- **THEN** the page renders no status cards and shows an informational message instead of an error

