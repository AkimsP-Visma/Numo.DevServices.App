namespace Numo.DevServices.Api.Features.ServiceData;

/// <param name="Filters">The caller's currently active filters. Most resources ignore them; a
/// nested resource's detail route needs the parent id one of them carries.</param>
public sealed record GetServiceDataRecordQuery(
    string Resource,
    Guid Id,
    IReadOnlyDictionary<string, string> Filters);

/// <summary>Rejects a filter the resource did not declare or a value malformed for its kind, the
/// same rules <see cref="GetServiceDataPageValidator"/> applies. Does not enforce
/// <see cref="FilterDescriptor.IsRequired"/> here: unlike the page route, a required filter is not
/// uniformly needed by every resource's detail fetch - di-pipeline-executions is nested for its
/// list but flat for its detail, so pipelineId is required to browse it but not to open one record.
/// A resource whose detail genuinely needs its parent id enforces that itself and fails with
/// <see cref="ServiceDataErrors.RequiredFilterMissing"/>, not a bare exception: verified live during
/// this feature's build, a missing required filter without that surfaced as an unhandled 500 with a
/// leaked stack trace.</summary>
public sealed class GetServiceDataRecordValidator : AbstractValidator<GetServiceDataRecordQuery>
{
    public GetServiceDataRecordValidator(ServiceDataCatalogue catalogue)
    {
        RuleFor(query => query.Resource).NotEmpty();
        RuleFor(query => query.Id).NotEmpty();

        RuleFor(query => query.Filters).Custom((filters, context) =>
        {
            var descriptor = catalogue.Find(context.InstanceToValidate.Resource)?.Descriptor;

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
        });
    }
}

public sealed class GetServiceDataRecordHandler(
    ServiceDataCatalogue catalogue,
    ILogger<GetServiceDataRecordHandler> logger)
{
    public async Task<NumoResult<ResourceRecord>> HandleAsync(
        GetServiceDataRecordQuery query,
        CancellationToken cancellationToken)
    {
        var resource = catalogue.Find(query.Resource);

        if (resource is null)
        {
            return NumoResult.Fail<ResourceRecord>(ServiceDataErrors.UnknownResource(query.Resource));
        }

        // No client library method takes a cancellation token, so this is the last point a cancelled
        // request can be dropped instead of being paid for.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var record = await resource.GetByIdAsync(query.Id, query.Filters, cancellationToken);

            return record is null
                ? NumoResult.Fail<ResourceRecord>(ServiceDataErrors.RecordNotFound(resource.Descriptor.Key, query.Id))
                : NumoResult.Ok(record);
        }
        catch (DownstreamCallException exception)
        {
            logger.LogWarning(
                exception,
                "Service data record {RecordId} of resource {ResourceKey} failed.",
                query.Id,
                resource.Descriptor.Key);

            return NumoResult.Fail<ResourceRecord>(exception.Error);
        }
    }
}
