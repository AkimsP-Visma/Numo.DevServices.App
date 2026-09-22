namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A pipeline's resources, nested under <c>/api/pipelines/{id}/resources</c>. Reachable only by
/// following the "Resources" relation from a <see cref="PipelinesResource"/> record.
///
/// The embedded resource's id points at a <see cref="ClientResourcesResource"/> record, but that
/// resource's own detail route is nested under clientId - a bare <see cref="RecordLink"/> cannot
/// carry both ids, so this emits a relation into the clientId-filtered list instead of a direct
/// link. The embedded clientId itself is flat (<see cref="ClientsResource"/>), so that one does
/// get a direct link.
/// </summary>
public sealed class PipelineResourcesResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-pipeline-resources";
    private const string ClientsResourceKey = "di-clients";
    private const string ClientResourcesResourceKey = "di-client-resources";
    private const string PipelineIdFilterKey = "pipelineId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Pipeline resources",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("resourceName", "Resource", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("clientId", "Client id", FieldKind.Guid, IsSortable: false),
        ],
        [
            new FilterDescriptor(PipelineIdFilterKey, "Pipeline id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(
            query,
            () => FetchAllAsync(RequirePipelineId(query.Filters), cancellationToken),
            ToRow);

    private async Task<IEnumerable<PipelineResource>> FetchAllAsync(Guid pipelineId, CancellationToken cancellationToken)
        => await api.GetAsync<List<PipelineResource>>(
            $"api/pipelines/{pipelineId}/resources",
            $"{ResourceKey} list",
            cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var pipelineId = RequirePipelineId(filters);

        var resource = await api.GetOrNullAsync<PipelineResource>(
            $"api/pipelines/{pipelineId}/resources/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return resource is null ? null : ToRecord(resource);
    }

    /// <summary>The page validator already rejects a request missing this before GetPageAsync
    /// runs; GetByIdAsync's caller does not, so this reports the same failure cleanly rather than
    /// throwing a bare exception that would surface as an unhandled 500.</summary>
    private static Guid RequirePipelineId(IReadOnlyDictionary<string, string> filters)
        => filters.TryGetValue(PipelineIdFilterKey, out var value) && Guid.TryParse(value, out var pipelineId)
            ? pipelineId
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, PipelineIdFilterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid pipelineId filter."));

    private static ResourceRow ToRow(PipelineResource resource)
        => new(
            resource.Id,
            DeletedAt: null,
            [
                new Cell(resource.Resource.Name, Link: null),
                new Cell(
                    resource.Resource.ClientId.ToString(),
                    new RecordLink(ClientsResourceKey, resource.Resource.ClientId)),
            ]);

    private static ResourceRecord ToRecord(PipelineResource resource)
        => new(
            resource.Id,
            string.IsNullOrWhiteSpace(resource.Resource.Name) ? resource.Id.ToString() : resource.Resource.Name,
            [
                new FieldValue("Id", resource.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Pipeline id", resource.PipelineId.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Resource name", resource.Resource.Name, FieldKind.Text, Link: null),
                new FieldValue(
                    "Client id",
                    resource.Resource.ClientId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(ClientsResourceKey, resource.Resource.ClientId)),
            ],
            [
                RelationDescriptor.To(
                    "Resources of this client",
                    ClientResourcesResourceKey,
                    "clientId",
                    resource.Resource.ClientId.ToString()),
            ]);
}

internal sealed record PipelineResource(Guid Id, Guid PipelineId, Guid ResourceId, EmbeddedResource Resource);

internal sealed record EmbeddedResource(Guid Id, Guid ClientId, string? Name);
