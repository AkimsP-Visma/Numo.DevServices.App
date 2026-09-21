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

        var column = descriptor.Columns.FirstOrDefault(candidate => IsSameKey(candidate.Key, sortColumn));

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
            var filter = descriptor.Filters.FirstOrDefault(candidate => IsSameKey(candidate.Key, key));

            if (filter is null)
            {
                context.AddFailure($"Resource '{descriptor.Key}' has no filter '{key}'.");
                continue;
            }

            if (!IsValueWellFormed(filter, value))
            {
                context.AddFailure($"Filter '{filter.Key}' of resource '{descriptor.Key}' cannot take '{value}'.");
            }
        }
    }

    private static bool IsValueWellFormed(FilterDescriptor filter, string value)
        => filter.Kind switch
        {
            FilterKind.Guid => System.Guid.TryParse(value, out _),
            FilterKind.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            FilterKind.Boolean => bool.TryParse(value, out _),
            FilterKind.Enum => filter.Options is not null
                && filter.Options.Any(option => IsSameKey(option, value)),
            _ => true,
        };

    private static bool IsSameKey(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

        var resourceQuery = new ResourceQuery(
            query.Page,
            query.PageSize,
            query.SortColumn,
            query.IsSortDescending,
            query.Filters.ToDictionary(filter => filter.Key, filter => filter.Value, StringComparer.OrdinalIgnoreCase));

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
}
