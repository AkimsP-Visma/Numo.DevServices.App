namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>The Configuration API's pipelines. No query parameters exist on this route at all, so
/// there is nothing to filter on.</summary>
public sealed class PipelinesResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-pipelines";
    private const string ConnectionsResourceKey = "di-connections";
    private const string PipelineResourcesResourceKey = "di-pipeline-resources";
    private const string PipelineExecutionsResourceKey = "di-pipeline-executions";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Pipelines",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("enabled", "Enabled", FieldKind.Boolean, IsSortable: false),
            new ColumnDescriptor("connectionId", "Connection id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("organizationId", "Organization id", FieldKind.Guid, IsSortable: false),
        ],
        [],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(query, () => FetchAllAsync(cancellationToken), ToRow);

    private async Task<IEnumerable<Pipeline>> FetchAllAsync(CancellationToken cancellationToken)
        => await api.GetAsync<List<Pipeline>>("api/pipelines", $"{ResourceKey} list", cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var pipeline = await api.GetOrNullAsync<Pipeline>(
            $"api/pipelines/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return pipeline is null ? null : ToRecord(pipeline);
    }

    private static ResourceRow ToRow(Pipeline pipeline)
        => new(
            pipeline.Id,
            DeletedAt: null,
            [
                new Cell(pipeline.Name, Link: null),
                new Cell(FieldFormat.Format(pipeline.Enabled), Link: null),
                new Cell(pipeline.ConnectionId.ToString(), new RecordLink(ConnectionsResourceKey, pipeline.ConnectionId)),
                new Cell(pipeline.OrganizationId?.ToString(), Link: null),
            ]);

    private static ResourceRecord ToRecord(Pipeline pipeline)
        => new(
            pipeline.Id,
            string.IsNullOrWhiteSpace(pipeline.Name) ? pipeline.Id.ToString() : pipeline.Name,
            [
                new FieldValue("Id", pipeline.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", pipeline.Name, FieldKind.Text, Link: null),
                new FieldValue("Enabled", FieldFormat.Format(pipeline.Enabled), FieldKind.Boolean, Link: null),
                new FieldValue(
                    "Connection id",
                    pipeline.ConnectionId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(ConnectionsResourceKey, pipeline.ConnectionId)),
                new FieldValue("Organization id", pipeline.OrganizationId?.ToString(), FieldKind.Guid, Link: null),
            ],
            [
                RelationDescriptor.To("Resources", PipelineResourcesResourceKey, "pipelineId", pipeline.Id.ToString()),
                RelationDescriptor.To("Executions", PipelineExecutionsResourceKey, "pipelineId", pipeline.Id.ToString()),
            ]);
}

internal sealed record Pipeline(Guid Id, string? Name, bool Enabled, Guid ConnectionId, Guid? OrganizationId);
