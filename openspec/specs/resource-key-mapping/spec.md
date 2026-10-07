# resource-key-mapping Specification

## Purpose
Bulk conversion between a DataIntegration client resource's connector keys and numo keys, in both
directions, over the Configuration API's read-only `numo-keys` and `connector-keys` routes. Covers
how the target is picked, how key input is read, how results map 1:1 onto input rows, and how the
service's silent empty answers become explicit errors or notices.

## Requirements

### Requirement: Pick the conversion target from the Configuration API's own records
The Key Mapping page SHALL let the user choose, in order, a client (from the Configuration API's
client list), one of that client's resources, and one of that resource's connectors. Each
connector option SHALL carry the connector's name and the resource connector's key fields. A
connector's name comes from `/api/connectors`, because `/connectors` under a resource returns only
a connector id. When a resource has exactly one connector, it SHALL be selected automatically. The
organization id SHALL be the app-wide tenant id, entered once in the same tenant field the
Personnel and DataIntegration browsers use and persisted across pages and reloads. The page
SHALL NOT have an organization field of its own. The service filters the loaded dataset by this
value, so without a well-formed one nothing can match.

#### Scenario: Choosing a client narrows the resource list
- **WHEN** the user selects a client
- **THEN** the resource picker offers only that client's resources, and any earlier resource and
  connector selection is cleared

#### Scenario: A single connector is preselected
- **WHEN** the selected resource has exactly one connector
- **THEN** that connector is selected without user action and its key fields define how
  Connector → Numo input lines are read

#### Scenario: Conversion is unavailable until the target is complete
- **WHEN** any of client, resource or connector is missing, or the tenant id is not a GUID
- **THEN** the convert action is disabled and no conversion request is sent

#### Scenario: Tenant id entered in a browser is reused
- **WHEN** a tenant id was entered on the Personnel or DataIntegration browser and the user then
  opens Key Mapping
- **THEN** Key Mapping shows that tenant id and converts against it as the organization id without
  it being entered again

### Requirement: The page is deep-linkable
The Key Mapping page SHALL read `clientId`, `resourceId` and `connector` (the connector name) from
its query string and SHALL keep them updated as the user changes the selection. That way a URL
reproduces the same target. The organization id SHALL NOT be part of the query string, because it
is the persisted app-wide tenant id.

#### Scenario: Opening a prefilled link
- **WHEN** the page is opened with `clientId` and `resourceId` query parameters
- **THEN** that client and resource are selected once their lists load, and the connector list for
  that resource is loaded

#### Scenario: A stale id in the link
- **WHEN** a query-string id does not match any loaded option
- **THEN** that picker is left empty rather than showing an id that cannot be converted against

### Requirement: Convert connector keys to numo keys in bulk
The system SHALL accept a batch of connector keys for the chosen target. Each key is a set of
values, one per key field of the chosen connector. The system SHALL return the numo key each one
maps to. Input SHALL be a text area with one connector key per line. For a connector with
several key fields, a line's values SHALL be split on tabs when the line contains one, else on
commas, else on runs of spaces, and SHALL be read in key-field order. For a connector with a
single key field, the whole line SHALL be the value and SHALL never be split, so a value may contain
spaces or commas. A line that yields a different number of values than there are key fields, or
an empty value, SHALL be flagged and SHALL block conversion rather than being guessed at.

#### Scenario: Pasting rows from a spreadsheet
- **WHEN** the user pastes three tab-separated lines for a connector with two key fields
- **THEN** three connector keys are read, each value assigned to the key field of its position

#### Scenario: Space-separated multi-field key
- **WHEN** a line reads `A1   B2` for a connector with two key fields
- **THEN** the key is read as `A1` and `B2`

#### Scenario: Wrong number of values
- **WHEN** a line yields one value for a connector with two key fields
- **THEN** that line is flagged and nothing is sent until it is fixed or removed

#### Scenario: A known key converts
- **WHEN** a submitted connector key exists in the loaded dataset for the target
- **THEN** its result row shows the numo key it maps to

### Requirement: Convert numo keys to connector keys in bulk
The system SHALL accept a batch of numo keys (GUIDs, one per line) for the chosen target. It SHALL
return the connector key each one maps to, shown as its key-field values.

#### Scenario: A malformed line is flagged before sending
- **WHEN** a line is not a GUID
- **THEN** the page marks that line invalid and sends nothing until it is fixed or removed

#### Scenario: A known numo key converts
- **WHEN** a submitted numo key exists in the loaded dataset for the target
- **THEN** its result row shows that record's value for every key field

### Requirement: Results map 1:1 to input rows
The result SHALL contain exactly one row per submitted input row, in submission order, including
repeated inputs. The service deduplicates its input, so the system SHALL map results back onto the
input rows itself. A key with no mapping SHALL appear in its row marked as not found. It SHALL
never be dropped.

#### Scenario: Duplicate input
- **WHEN** the same key is submitted on two rows
- **THEN** both rows appear in the result with the same converted value

#### Scenario: Unmapped key
- **WHEN** a submitted key has no mapping in the loaded dataset
- **THEN** its row is present and marked "not found"

### Requirement: Silent service failures become explicit
Before calling the service, the system SHALL verify two things: that the chosen connector is
attached to the chosen resource with at least one key field, and that every submitted connector
key carries a non-blank value for each key field. The service answers either problem with an empty
or all-null result rather than an error, so the system SHALL report each one as its own error id.
When a valid conversion matches nothing at all, the result SHALL carry a notice. The notice SHALL
name the likely cause: no loaded dataset exists for that client, resource, connector and
organization.

#### Scenario: Missing key field value
- **WHEN** a submitted connector key leaves one key field blank
- **THEN** the request fails with the `KeyFieldValueMissing` error id and the Configuration API is
  not called for the conversion

#### Scenario: Connector not attached to the resource
- **WHEN** the submitted connector name is not one of the resource's connectors
- **THEN** the request fails with the `ConnectorNotAttached` error id

#### Scenario: Nothing matched
- **WHEN** every result row is not found
- **THEN** the result carries a notice pointing at a missing loaded dataset or a wrong
  organization id, so an all-miss result is not read as "these keys do not exist"

### Requirement: Bounded batch size
A single conversion SHALL accept at most 1000 input rows. The service streams the whole loaded
dataset per call whatever the batch size, so larger inputs SHALL be split by the user rather than
silently truncated.

#### Scenario: Oversized batch
- **WHEN** more than 1000 rows are submitted
- **THEN** the request fails validation and names the limit

### Requirement: Stable error ids for the key-mapping slice
Every failure of this slice SHALL carry an id from `KeyMappingErrors`, following the
platform-wide rule in `platform-conventions`. The ids are: `ConnectorNotAttached`,
`KeyFieldValueMissing`, `DownstreamCallFailed`. Malformed input, such as a non-GUID or an
oversized batch, fails request validation the same way every other slice's validators do.

#### Scenario: Unreachable Configuration API
- **WHEN** the Configuration API call fails or times out
- **THEN** the response is a problem-details failure carrying the `DownstreamCallFailed` id

### Requirement: Fixed routes over a hand-rolled client, never a pass-through
Every key-mapping endpoint SHALL map to a fixed Configuration API route with typed path segments,
never a caller-supplied path, following the `platform-conventions` pass-through rule. The slice
SHALL reach the Configuration API over its own hand-rolled `HttpClient` rather than
`Numo.DataIntegration.Configuration.Lib`. This is the documented exception to the
`platform-conventions` client-library preference. The library's `GetResourceNumoKeysAsync` and
`GetResourceConnectorKeysAsync` take client and resource *names* and re-resolve them with an extra
`GetResourceByName` call, while this page already holds ids. The library is also referenced
nowhere else in this app.

#### Scenario: Conversion reaches only the fixed route
- **WHEN** a conversion is requested for a client id and resource id
- **THEN** the only downstream conversion call is a POST to
  `api/clients/{clientId}/resources/{resourceId}/numo-keys` or `.../connector-keys` with
  `connectorName` and `organizationId` as query parameters
