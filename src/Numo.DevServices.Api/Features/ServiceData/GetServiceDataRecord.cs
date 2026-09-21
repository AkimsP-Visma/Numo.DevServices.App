namespace Numo.DevServices.Api.Features.ServiceData;

public sealed record GetServiceDataRecordQuery(string Resource, Guid Id);

public sealed class GetServiceDataRecordValidator : AbstractValidator<GetServiceDataRecordQuery>
{
    public GetServiceDataRecordValidator()
    {
        RuleFor(query => query.Resource).NotEmpty();
        RuleFor(query => query.Id).NotEmpty();
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
            var record = await resource.GetByIdAsync(query.Id, cancellationToken);

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
