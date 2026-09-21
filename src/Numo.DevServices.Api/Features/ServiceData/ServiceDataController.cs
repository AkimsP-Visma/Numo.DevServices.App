namespace Numo.DevServices.Api.Features.ServiceData;

[ApiController]
[Route("api/service-data")]
public sealed class ServiceDataController(INumoMediator mediator) : ControllerBase
{
    private const string FilterKeyPrefix = "filters[";

    /// <summary>
    /// No tenant filter: the catalogue is compile-time metadata, and the frontend needs it to build
    /// the resource picker before a tenant id has been typed in.
    /// </summary>
    [HttpGet]
    public Task<NumoResult<GetServiceDataResourcesResult>> GetResources(CancellationToken cancellationToken)
        => mediator.SendAsync<GetServiceDataResourcesResult>(new GetServiceDataResourcesQuery(), cancellationToken);

    /// <summary>Filters arrive as <c>filters[key]=value</c>, keyed by
    /// <see cref="FilterDescriptor.Key"/>.</summary>
    [HttpGet("{resource}")]
    [TypeFilter(typeof(TenantIdActionFilter))]
    public Task<NumoResult<ResourcePage>> GetPage(
        string resource,
        CancellationToken cancellationToken,
        [FromQuery] int page = GetServiceDataPageQuery.FirstPage,
        [FromQuery] int pageSize = GetServiceDataPageQuery.DefaultPageSize,
        [FromQuery] string? sortColumn = null,
        [FromQuery] bool isSortDescending = false)
        => mediator.SendAsync<ResourcePage>(
            new GetServiceDataPageQuery(
                resource,
                page,
                pageSize,
                sortColumn,
                isSortDescending,
                ReadFilters(Request.Query)),
            cancellationToken);

    [HttpGet("{resource}/{id:guid}")]
    [TypeFilter(typeof(TenantIdActionFilter))]
    public Task<NumoResult<ResourceRecord>> GetRecord(
        string resource,
        Guid id,
        CancellationToken cancellationToken)
        => mediator.SendAsync<ResourceRecord>(new GetServiceDataRecordQuery(resource, id), cancellationToken);

    /// <summary>
    /// Read by hand rather than model-bound: given no <c>filters[...]</c> key at all, the dictionary
    /// binder falls back to the bare query string and swallows the paging parameters as filters.
    /// </summary>
    private static Dictionary<string, string> ReadFilters(IQueryCollection query)
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in query)
        {
            if (!key.StartsWith(FilterKeyPrefix, StringComparison.OrdinalIgnoreCase) || !key.EndsWith(']'))
            {
                continue;
            }

            var filterKey = key[FilterKeyPrefix.Length..^1];

            if (filterKey.Length > 0)
            {
                filters[filterKey] = value.ToString();
            }
        }

        return filters;
    }
}
