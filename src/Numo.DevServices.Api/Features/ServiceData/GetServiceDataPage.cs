using System.Globalization;
using FluentValidation.Results;

namespace Numo.DevServices.Api.Features.ServiceData;

public sealed record GetServiceDataPageQuery(
    string Resource,
    int Page,
    int PageSize,
    string? SortColumn,
    bool IsSortDescending,
    IReadOnlyDictionary<string, string> Filters)
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 20;
    public const int MinPageSize = 1;

    // The services honour any page size, so the ceiling is this tool's own: a browsable grid, not a
    // full-table read against a live service.
    public const int MaxPageSize = 200;
}

/// <summary>
/// Descriptor lookups shared by the validator, which rejects what a resource did not declare, and the
/// handler, which substitutes the declared spelling of what it did.
/// </summary>
internal static class DeclaredKey
{
    public static ColumnDescriptor? FindColumn(ResourceDescriptor descriptor, string? key)
        => descriptor.Columns.FirstOrDefault(column => IsSame(column.Key, key));

    public static FilterDescriptor? FindFilter(ResourceDescriptor descriptor, string key)
        => descriptor.Filters.FirstOrDefault(filter => IsSame(filter.Key, key));

    public static bool IsSame(string left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>Shared by the page and record validators, so a value malformed for its kind is
    /// rejected the same way regardless of which route it arrived on.</summary>
    public static bool IsValueWellFormed(FilterDescriptor filter, string value)
        => filter.Kind switch
        {
            FilterKind.Guid => Guid.TryParse(value, out _),
            FilterKind.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            FilterKind.Boolean => bool.TryParse(value, out _),
            FilterKind.Enum => filter.Options is not null
                && filter.Options.Any(option => IsSame(option, value)),
            FilterKind.GuidList => IsGuidListWellFormed(value),
            _ => true,
        };

    /// <summary>
    /// The count is checked here and not in the resource so that an overlong list is a stated 400
    /// rather than a downstream request the client library silently turns into a POST search.
    ///
    /// An all-zero GUID is rejected for the same reason the reader discards it: the services ignore
    /// an empty id array, so a list of nothing but zeroes would otherwise reach a service as no
    /// filter at all and answer with the whole table. A list that splits to nothing is rejected on
    /// the same ground.
    /// </summary>
    private static bool IsGuidListWellFormed(string value)
    {
        var parts = ResourceQueryFilters.SplitList(value);

        return parts.Length > 0
            && parts.Length <= ResourceQueryFilters.MaxListValues
            && parts.All(part => Guid.TryParse(part, out var id) && id != Guid.Empty);
    }
}

/// <summary>
/// Keeps anything the descriptor does not declare from reaching a service. Rules that need the
/// descriptor stay silent for an unknown resource key, which is the handler's
/// <see cref="ServiceDataErrors.UnknownResource"/> failure and not a validation problem.
/// </summary>
public sealed class GetServiceDataPageValidator : AbstractValidator<GetServiceDataPageQuery>
{
    public GetServiceDataPageValidator(ServiceDataCatalogue catalogue)
    {
        RuleFor(query => query.Resource).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(GetServiceDataPageQuery.FirstPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(GetServiceDataPageQuery.MinPageSize, GetServiceDataPageQuery.MaxPageSize);

        RuleFor(query => query.SortColumn).Custom((sortColumn, context) =>
            ValidateSortColumn(sortColumn, Descriptor(catalogue, context), context));

        RuleFor(query => query.Filters).Custom((filters, context) =>
            ValidateFilters(filters, Descriptor(catalogue, context), context));
    }

    private static ResourceDescriptor? Descriptor(
        ServiceDataCatalogue catalogue,
        ValidationContext<GetServiceDataPageQuery> context)
        => catalogue.Find(context.InstanceToValidate.Resource)?.Descriptor;

    private static void ValidateSortColumn(
        string? sortColumn,
        ResourceDescriptor? descriptor,
        ValidationContext<GetServiceDataPageQuery> context)
    {
        if (string.IsNullOrWhiteSpace(sortColumn) || descriptor is null)
        {
            return;
        }

        var column = DeclaredKey.FindColumn(descriptor, sortColumn);

        if (column is null)
        {
            context.AddFailure($"Resource '{descriptor.Key}' has no column '{sortColumn}'.");
            return;
        }

        // The services silently ignore an order on a column they do not mark orderable, which would
        // show as a sort that does nothing.
        if (!column.IsSortable)
        {
            context.AddFailure($"Column '{column.Key}' of resource '{descriptor.Key}' cannot be sorted on.");
        }
    }

    private static void ValidateFilters(
        IReadOnlyDictionary<string, string> filters,
        ResourceDescriptor? descriptor,
        ValidationContext<GetServiceDataPageQuery> context)
    {
        if (descriptor is null)
        {
            return;
        }

        foreach (var (key, value) in filters)
        {
            var filter = DeclaredKey.FindFilter(descriptor, key);

            if (filter is null)
            {
                context.AddFailure($"Resource '{descriptor.Key}' has no filter '{key}'.");
                continue;
            }

            if (!DeclaredKey.IsValueWellFormed(filter, value))
            {
                context.AddFailure($"Filter '{filter.Key}' of resource '{descriptor.Key}' cannot take '{value}'.");
            }
        }

        // A nested resource (di-client-resources and its siblings) cannot be browsed without the
        // parent id its route needs, so a request missing it is a 400 rather than a downstream 404.
        foreach (var requiredFilter in descriptor.Filters.Where(filter => filter.IsRequired))
        {
            if (!filters.ContainsKey(requiredFilter.Key))
            {
                context.AddFailure(
                    $"Resource '{descriptor.Key}' requires filter '{requiredFilter.Key}' to be set.");
            }
        }
    }
}

public sealed class GetServiceDataPageHandler(
    ServiceDataCatalogue catalogue,
    ILogger<GetServiceDataPageHandler> logger)
{
    public async Task<NumoResult<ResourcePage>> HandleAsync(
        GetServiceDataPageQuery query,
        CancellationToken cancellationToken)
    {
        var resource = catalogue.Find(query.Resource);

        if (resource is null)
        {
            return NumoResult.Fail<ResourcePage>(ServiceDataErrors.UnknownResource(query.Resource));
        }

        // No client library method takes a cancellation token, so this is the last point a cancelled
        // request can be dropped instead of being paid for.
        cancellationToken.ThrowIfCancellationRequested();

        var resourceQuery = ToResourceQuery(query, resource.Descriptor);

        try
        {
            return NumoResult.Ok(await resource.GetPageAsync(resourceQuery, cancellationToken));
        }
        catch (DownstreamCallException exception)
        {
            logger.LogWarning(exception, "Service data page for resource {ResourceKey} failed.", resource.Descriptor.Key);
            return NumoResult.Fail<ResourcePage>(exception.Error);
        }
    }

    /// <summary>
    /// Keys are matched ignoring case but must reach a service in their declared spelling: a service
    /// silently ignores <c>OrderBy=FIRSTNAME</c>, which is the very failure the sortable check exists
    /// to prevent, and a resource reading a filter by its exact key would miss a differently cased one.
    /// </summary>
    private static ResourceQuery ToResourceQuery(GetServiceDataPageQuery query, ResourceDescriptor descriptor)
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in query.Filters)
        {
            // An undeclared key never gets this far: the validator has already failed the request.
            var filter = DeclaredKey.FindFilter(descriptor, key);

            if (filter is not null)
            {
                filters[filter.Key] = value;
            }
        }

        return new ResourceQuery(
            query.Page,
            query.PageSize,
            DeclaredKey.FindColumn(descriptor, query.SortColumn)?.Key,
            query.IsSortDescending,
            filters);
    }
}
