# resource-key-mapping design

## How it works
The page picks a client, one of its resources and one of that resource's connectors. Each comes
from a fixed lookup route under `api/key-mappings/clients`. A batch of keys is then sent to one of
two conversions, which call the Configuration API's read-only routes:

| Downstream route | Body | Answer |
|---|---|---|
| `POST api/clients/{clientId}/resources/{resourceId}/numo-keys?connectorName&organizationId` | `[{ "value": { "<keyField>": "<value>" } }]` | `[{ connectorKey, numoKey? }]` |
| `POST api/clients/{clientId}/resources/{resourceId}/connector-keys?connectorName&organizationId` | `[guid]` | `[{ numoKey, connectorKeys? }]` |

Both are query handlers in the Configuration API. They stream the one loaded dataset filtered by
client, resource, connector name and the records' `OrganizationId`, so nothing is ever written. A
connector's key fields come from the resource's `/connectors`, which carries only a connector id,
so connector names come from `api/connectors`. The service looks connectors up by name. The
organization id is the app-wide tenant id from the shared tenant field in the page header; on
Testing, departments read with a tenant id convert back with that same value as `organizationId`.

## Decisions
- **Its own slice, not part of service data browsing.** That slice's contract is a descriptor-driven
  list of records, and a form that posts a batch and returns a mapping fits neither its descriptors
  nor its pages. The slice registers its own `HttpClient` against the same discovery key and has its
  own error ids, at the cost of a few duplicated registration lines.
- **Hand-rolled HTTP rather than the Configuration client library.** The library has both
  conversions, but they take client and resource names and re-resolve the resource by name with an
  extra call, while this page already holds ids. This is the documented exception to the
  `platform-conventions` client-library rule.
- **The backend checks what the service would silently swallow.** The service answers an unknown or
  unattached connector, a connector without key fields, or a key missing a key field with `[]` or
  all-null instead of an error. The conversion first resolves the connector by name to its key fields
  and fails with `ConnectorNotAttached` or `KeyFieldValueMissing`. After those checks the only cause
  left for an all-miss result is the dataset (missing, duplicated, or the wrong organization), so
  that result carries a notice saying so.
- **Results are mapped 1:1 onto input rows in the backend.** The service deduplicates its input, so
  each input row, duplicates included, is looked up in a map built from the answer. Connector keys
  are matched by their values in key-field order, reading the service's field names
  case-insensitively, and numo keys by GUID. An answer that can't be matched back counts as not
  found, never as someone else's mapping.
- **A connector key crosses this app's API as its values in key-field order, never as a
  dictionary.** No JSON naming policy, this app's or the service's, can then rename a key field away
  from its value.
- **Input is one key per line in a text area, kept separately per direction.** A multi-field line
  splits on tabs if it has one (a spreadsheet copy), otherwise on commas, otherwise on runs of
  spaces. A single-field line is never split, so its value may contain spaces or commas. A line with
  the wrong number of values is flagged and blocks conversion rather than being guessed at.
- **A batch is capped at 1000 rows.** The service streams the whole dataset per call whatever the
  batch size, so the cap bounds the request and the result table, not downstream work.
- **The URL keeps client, resource and connector, replacing history entries while picking.** A
  link reproduces the target. The organization id stays out of the URL, since the tenant id
  persists app-wide. A client-resource record links here prefilled through the record's tool links.

## Approaches that don't work
- **An editable grid with one input per key field.** Entering many keys that way is harder than
  pasting lines.

## Constraints and limitations
- An all-null connector-keys answer can mean either no dataset or keys that genuinely don't exist;
  the notice names both.
- No route lists organization ids, so a wrong tenant id shows only as "nothing matched".
- A connector missing from the connector list has no name to send, so it shows its id and can't be
  converted against.
