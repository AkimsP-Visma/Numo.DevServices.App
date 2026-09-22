namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// An execution's steps, nested under <c>/api/pipeline-executions/{executionId}/steps</c>.
/// Reachable only by following the "Steps" relation from a
/// <see cref="PipelineExecutionsResource"/> record.
///
/// The list route's schema (<c>PipelineExecutionStep</c>) and the detail route's schema
/// (<c>PipelineExecutionStepResponse</c>) are not the same shape downstream - the detail schema
/// omits status, startTime, endTime and errorMessage entirely, so the detail page shows less than
/// the row it was opened from. That is the service's own inconsistency, not a bug here: this
/// resource renders whatever each route actually returns.
///
/// status crosses the wire as a bare integer with no enum names available anywhere reachable
/// (see the same note on <see cref="PipelineExecutionsResource"/>), so it renders as
/// <see cref="FieldKind.Number"/>.
/// </summary>
public sealed class ExecutionStepsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-execution-steps";
    private const string ConnectionsResourceKey = "di-connections";
    private const string DatasetResourceKey = "di-execution-step-dataset";
    private const string ExecutionIdFilterKey = "executionId";
    private const string StepIdFilterKey = "stepId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Execution steps",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("pipelineResourceId", "Pipeline resource id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("connectionId", "Connection id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("status", "Status", FieldKind.Number, IsSortable: false),
            new ColumnDescriptor("startTime", "Start time", FieldKind.DateTime, IsSortable: false),
            new ColumnDescriptor("endTime", "End time", FieldKind.DateTime, IsSortable: false),
            new ColumnDescriptor("errorMessage", "Error message", FieldKind.Text, IsSortable: false),
        ],
        [
            new FilterDescriptor(ExecutionIdFilterKey, "Execution id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(
            query,
            () => FetchAllAsync(RequireExecutionId(query.Filters), cancellationToken),
            ToRow);

    private async Task<IEnumerable<PipelineExecutionStep>> FetchAllAsync(Guid executionId, CancellationToken cancellationToken)
        => await api.GetAsync<List<PipelineExecutionStep>>(
            $"api/pipeline-executions/{executionId}/steps",
            $"{ResourceKey} list",
            cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var executionId = RequireExecutionId(filters);

        var step = await api.GetOrNullAsync<PipelineExecutionStepDetail>(
            $"api/pipeline-executions/{executionId}/steps/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return step is null ? null : ToRecord(step);
    }

    /// <summary>The page validator already rejects a request missing this before GetPageAsync
    /// runs; GetByIdAsync's caller does not, so this reports the same failure cleanly rather than
    /// throwing a bare exception that would surface as an unhandled 500.</summary>
    private static Guid RequireExecutionId(IReadOnlyDictionary<string, string> filters)
        => filters.TryGetValue(ExecutionIdFilterKey, out var value) && Guid.TryParse(value, out var executionId)
            ? executionId
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, ExecutionIdFilterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid executionId filter."));

    private static ResourceRow ToRow(PipelineExecutionStep step)
        => new(
            step.Id,
            DeletedAt: null,
            [
                new Cell(step.PipelineResourceId.ToString(), Link: null),
                new Cell(step.ConnectionId.ToString(), new RecordLink(ConnectionsResourceKey, step.ConnectionId)),
                new Cell(FieldFormat.FormatNumber(step.Status), Link: null),
                new Cell(FieldFormat.Format(step.StartTime), Link: null),
                new Cell(FieldFormat.Format(step.EndTime), Link: null),
                new Cell(step.ErrorMessage, Link: null),
            ]);

    private static ResourceRecord ToRecord(PipelineExecutionStepDetail step)
        => new(
            step.Id,
            step.Id.ToString(),
            [
                new FieldValue("Id", step.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Execution id", step.ExecutionId.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Pipeline resource id", step.PipelineResourceId.ToString(), FieldKind.Guid, Link: null),
                new FieldValue(
                    "Connection id",
                    step.ConnectionId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(ConnectionsResourceKey, step.ConnectionId)),
            ],
            [
                new RelationDescriptor(
                    "Dataset",
                    DatasetResourceKey,
                    new Dictionary<string, string>
                    {
                        [ExecutionIdFilterKey] = step.ExecutionId.ToString(),
                        [StepIdFilterKey] = step.Id.ToString(),
                    }),
            ]);
}

internal sealed record PipelineExecutionStep(
    Guid Id,
    Guid ExecutionId,
    Guid PipelineResourceId,
    Guid ConnectionId,
    int Status,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    string? ErrorMessage);

/// <summary>The detail route's own, narrower shape - see the class-level remark.</summary>
internal sealed record PipelineExecutionStepDetail(
    Guid Id,
    Guid ExecutionId,
    Guid PipelineResourceId,
    Guid ConnectionId);
