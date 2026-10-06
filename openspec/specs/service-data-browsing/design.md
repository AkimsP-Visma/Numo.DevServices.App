# service-data-browsing design

## How it works
One backend slice serves two nav entries, the Personnel Browser (Person and Employee records) and
the DataIntegration Browser (the Configuration API's clients, pipelines, connections and more).
Each browsable resource is one class that declares a descriptor (columns, filters, section,
whether it needs a tenant) and fetches its own pages and records. The frontend renders every
resource with the same generic grid and record page, driven only by those descriptors, so a new
list of records is a new backend class and no new frontend code. A record can also carry tool
links that open another page of this app with the record's ids, which the record page renders
without knowing which resource produced them.

## Decisions
- **No client library for any DataIntegration resource.** The Configuration API's client library is
  built for point lookups by name. It has no list method for any browsed resource and no by-id
  method for the nested certificates or credentials routes, so browsing on it would mean bolting
  list semantics onto an API without them. Every DataIntegration resource reads the Configuration
  API over one shared hand-rolled `HttpClient`. It answers plain JSON with no envelope and needs no
  tenant header.
- **No client library for `department-roles`, for a different reason.** Its DTO is internal to the
  Employee library and no client method covers the unfiltered roles route: a gap in that library
  only, since every other Personnel resource uses one. This resource owns its own client, record,
  envelope parsing and tenant header. Parsing the envelope by hand also lets it recognise "not
  found" from the error's structured metadata rather than from message wording.
- **Two paging strategies, chosen by what each source can do.** Person and Employee routes page by
  index but report no total anywhere, so `HasMore` comes from fetching page N+1 at the same size
  and testing it for emptiness. DataIntegration routes don't page at all; their sets are
  configuration-sized, so each is fetched once and sliced in memory. The execution step dataset
  pages by opaque continuation token, so it fetches one batch, never offers Next, and says in a
  notice when the service's token shows more.
- **The tenant requirement is per resource.** Person and Employee answer anonymously given
  `Numo-Tenant-Id` and 401 without it; the Configuration API needs none. A slice-wide rule would
  either force a tenant on data that doesn't need one or omit it where it's required. The frontend
  sends no request for a tenant-requiring resource until a tenant id is entered, because the
  services answer an unknown but well-formed tenant with an empty list, which would read as "no
  data" rather than "nothing asked yet". The tenant id is one app-wide value, persisted in the
  browser and shared with Key Mapping, which sends it as the organization id.
- **The picker leaves out any resource that can't stand alone.** That is a resource marked reachable
  only by relation, or one with a required filter (a parent id only a relation supplies). Opened
  directly, either would show nothing.
- **Relations render inline under their parent record.** A plain relation loads automatically. A
  relation into a resource reachable only by relation stays behind a button, so its records are
  never fetched speculatively. Every panel previews ten rows and links to the full list for paging,
  sorting and filtering.
- **A legal relation id becomes two relations, "Positions" and "Absences".** The Employee library has
  no legal relation entity or route, not even a name, so a link would point at a resource that
  doesn't exist, and a standalone list would invent one.
- **Credentials and certificates are gated; parameters are not.** All three come through the same
  `expand` on a connection's detail route. Credentials and certificates carry secrets, so they are
  reachable only by relation. Parameters were verified live to carry only plain configuration (a
  `RestUrl`), so they load like any relation. The connection list never sends `expand`, and its
  record has no property for either secret, so no expansion can leak one into a list cell.
- **The execution step dataset is gated for personal data.** A dataset is whatever its pipeline
  extracted, and a live probe found a pipeline whose dataset carries person-linked fields. It has no
  fixed schema, so its record shows every field it carries.

## Approaches that don't work
- **Asking for one extra row (`pageSize + 1`) to detect a next page.** The server's window is a
  function of the page size, so inflating it shifts the next page's start and silently skips a
  record at every boundary.

## Constraints and limitations
- The unpaged DataIntegration fetch stops being reasonable if any of those lists grows beyond
  configuration size.
- A dataset record past the first batch can't be opened directly: there is no by-id route, so the
  record page searches the first batch.
- No endpoint is authorized while this slice reads real personal data and, behind gated relations,
  real secrets. It is accepted only because the app is network-internal and developer-only. This
  slice is the first that must gain `[Authorize]`.
