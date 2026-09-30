# environment-switching Specification

## Purpose
Tracks which deployment environment (Testing, Staging, Production, Local) this tool currently
points at, and supplies each service's location for that environment by replacing the default
`IServiceDiscoveryService` with an environment-aware implementation. The active environment lives
only in memory (reset to the configured default on restart) and is switched via a frontend picker
in the app layout plus a small read/switch API.
## Requirements
### Requirement: Configured environment set
The system SHALL read a fixed set of named deployment environments from the `Environments` configuration section, each with its own map of Numo service ids to service locations, and a `Default` key naming which environment is active on startup. As shipped, `appsettings.json` defines four environments: `Testing`, `Staging`, `Production`, and `Local`, with `Default` set to `Testing`. This is not a closed, hardcoded list in code - it is whatever `Environments:Definitions` contains in configuration.

#### Scenario: Environments load from configuration at startup
- **WHEN** the application starts
- **THEN** `EnvironmentsOptions` is bound from the `Environments` configuration section, and the active environment is initialized to `EnvironmentsOptions.Default`

#### Scenario: Each environment has its own service location map
- **WHEN** the `Testing`, `Staging`, `Production`, and `Local` environments are each configured
- **THEN** each one independently maps Numo service ids (for example `Numo.Person.Api`) to a `Location` (and optionally an `AppId`), and an environment may omit a service id that another environment defines (for example `Local` omits `Numo.Authorization.Api`)

### Requirement: Active environment tracking
The system SHALL track exactly one currently active environment in memory, for the lifetime of the running process, with no persistence to disk, a cookie, or a database. On every application restart the active environment SHALL reset to `EnvironmentsOptions.Default`.

#### Scenario: Active environment survives for the process lifetime
- **WHEN** the active environment is switched during a running session
- **THEN** subsequent reads of the active environment return the switched-to value until the process restarts

#### Scenario: Active environment resets on restart
- **WHEN** the application process restarts
- **THEN** the active environment is the configured `Default`, regardless of what was active before the restart

### Requirement: Read and switch the active environment
The system SHALL expose the active environment and the full known set over `GET /api/environments`, and SHALL allow switching the active environment over `PUT /api/environments/current`, accepting only one of the configured environment keys.

#### Scenario: Reading the current environment
- **WHEN** a client sends `GET /api/environments`
- **THEN** the response includes the currently active environment key and the list of every configured environment key, in the order `appsettings` declares them

#### Scenario: Switching to a known environment
- **WHEN** a client sends `PUT /api/environments/current` with a key that matches a configured environment
- **THEN** that environment becomes active and the response confirms the new current environment

#### Scenario: Switching to an unknown environment is rejected
- **WHEN** a client sends `PUT /api/environments/current` with a key that does not match any configured environment
- **THEN** the request fails with a stable `NumoError` identifying the key as not a configured environment, and the active environment is left unchanged

### Requirement: Environment-aware service discovery
The system SHALL resolve every Numo service location through `IServiceDiscoveryService`, backed by an implementation that reads the currently active environment's own service map instead of a single flat configuration section, so that every existing caller of the interface becomes environment-aware without being modified.

#### Scenario: Service location resolves against the active environment
- **WHEN** a caller resolves the location of a service id that is configured under the currently active environment
- **THEN** the location returned is the one configured for that service under that specific environment, not any other environment's value for the same service id

#### Scenario: Switching environments changes resolution immediately
- **WHEN** the active environment is switched and a caller then resolves a service location
- **THEN** the location reflects the newly active environment's configuration, with no caching of the previous environment's value

#### Scenario: Unconfigured service in the active environment fails explicitly
- **WHEN** a caller resolves a service id that is absent from the active environment's service map, or present with a blank location
- **THEN** the lookup throws `ServiceDoesNotExistException` naming the service id and the active environment, rather than returning null

#### Scenario: No environment provides an application id
- **WHEN** a caller requests a service's application id
- **THEN** the lookup throws `ServiceDoesNotExistException`, since no configured environment defines an `AppId`

### Requirement: Frontend environment picker
The system SHALL offer a picker in the application's header/layout that shows the active environment and every known environment, and lets the user switch it. Switching SHALL take effect by calling the switch endpoint and then reloading the page, rather than invalidating per-component caches.

#### Scenario: Picker shows known environments
- **WHEN** the application layout loads
- **THEN** it fetches the current environment and the known set from the backend and offers them in a selector

#### Scenario: Selecting a different environment reloads the app
- **WHEN** a user selects a different environment in the picker
- **THEN** the frontend calls the switch endpoint, and on success reloads the page so no component is left holding data fetched under the previous environment

