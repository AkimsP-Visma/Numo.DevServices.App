namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A pipeline's executions. The list route is nested under the pipeline
/// (<c>/api/pipelines/{id}/executions</c>); the saved OpenAPI spec types the detail route
/// (<c>/api/pipeline-executions/{executionId}</c>) as returning an *array* of
/// <see cref="PipelineExecution"/>, but a live probe against test.numo.lv confirmed the real
/// response is a bare object - a Swashbuckle mis-inference in the spec. <see cref="GetByIdAsync"/>
/// still reads through <see cref="DataIntegrationConfigurationApi.GetOrNullEitherShapeAsync{T}"/>
/// rather than assuming the object shape everywhere, since the spec proved unreliable once already.
///
/// state and result cross the wire as bare integers with no enum names in the OpenAPI spec (no
/// x-enumNames) and no reachable client type (see <see cref="DataIntegrationConfigurationApi"/>).
/// <see cref="PipelineExecutionState"/> and <see cref="PipelineExecutionResult"/> below are not
/// guessed: they're copied from Numo.DataIntegration.Configuration.Api's own domain model
/// (src/Numo.DataIntegration.Configuration.Domain/PipelineExecutions/PipelineExecution.cs in that
/// repo, read directly on 2026-09-23), matching its member names and numeric values exactly, since
/// this project has no dependency on that domain assembly to share the type from. The state filter
/// stays dropped even so - fetching this resource still means fetching every execution and slicing
/// in memory (see <see cref="ResourcePageBuilder.BuildUnpagedAsync{TItem}"/>), and this resource does
/// not yet filter that in-memory set by anything but the required pipelineId.
/// </summary>
public sealed class PipelineExecutionsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-pipeline-executions";
    private const string PipelinesResourceKey = "di-pipelines";
    private const string ExecutionStepsResourceKey = "di-execution-steps";
    private const string PipelineIdFilterKey = "pipelineId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Pipeline executions",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("pipelineId", "Pipeline id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("state", "State", FieldKind.Enum, IsSortable: false),
            new ColumnDescriptor("startTime", "Start time", FieldKind.DateTime, IsSortable: false),
            new ColumnDescriptor("endTime", "End time", FieldKind.DateTime, IsSortable: false),
            new ColumnDescriptor("result", "Result", FieldKind.Enum, IsSortable: false),
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

    private async Task<IEnumerable<PipelineExecution>> FetchAllAsync(Guid pipelineId, CancellationToken cancellationToken)
        => await api.GetAsync<List<PipelineExecution>>(
            $"api/pipelines/{pipelineId}/executions",
            $"{ResourceKey} list",
            cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var execution = await api.GetOrNullEitherShapeAsync<PipelineExecution>(
            $"api/pipeline-executions/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return execution is null ? null : ToRecord(execution);
    }

    /// <summary>Only GetPageAsync calls this - GetByIdAsync's route is flat (see the class-level
    /// remark) and needs no pipelineId at all. The page validator already rejects a request
    /// missing this before GetPageAsync runs, so this is a defensive assertion, not a reachable
    /// user-facing failure.</summary>
    private static Guid RequirePipelineId(IReadOnlyDictionary<string, string> filters)
        => filters.TryGetValue(PipelineIdFilterKey, out var value) && Guid.TryParse(value, out var pipelineId)
            ? pipelineId
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, PipelineIdFilterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid pipelineId filter."));

    private static ResourceRow ToRow(PipelineExecution execution)
        => new(
            execution.Id,
            DeletedAt: null,
            [
                new Cell(execution.PipelineId.ToString(), new RecordLink(PipelinesResourceKey, execution.PipelineId)),
                new Cell(FieldFormat.Format(execution.State), Link: null),
                new Cell(FieldFormat.Format(execution.StartTime), Link: null),
                new Cell(FieldFormat.Format(execution.EndTime), Link: null),
                new Cell(FieldFormat.Format(execution.Result), Link: null),
            ]);

    private static ResourceRecord ToRecord(PipelineExecution execution)
        => new(
            execution.Id,
            execution.Id.ToString(),
            [
                new FieldValue("Id", execution.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue(
                    "Pipeline id",
                    execution.PipelineId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(PipelinesResourceKey, execution.PipelineId)),
                new FieldValue("State", FieldFormat.Format(execution.State), FieldKind.Enum, Link: null),
                new FieldValue("Start time", FieldFormat.Format(execution.StartTime), FieldKind.DateTime, Link: null),
                new FieldValue("End time", FieldFormat.Format(execution.EndTime), FieldKind.DateTime, Link: null),
                new FieldValue("Result", FieldFormat.Format(execution.Result), FieldKind.Enum, Link: null),
            ],
            [
                RelationDescriptor.To("Steps", ExecutionStepsResourceKey, "executionId", execution.Id.ToString()),
            ]);
}

internal sealed record PipelineExecution(
    Guid Id,
    Guid PipelineId,
    PipelineExecutionState State,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    PipelineExecutionResult Result);

/// <summary>Copied member-for-member (name and numeric value) from
/// Numo.DataIntegration.Configuration.Api's own domain enum - see the class-level remark above for
/// exactly where and why this project keeps its own copy instead of a shared reference.</summary>
internal enum PipelineExecutionState
{
    Pending = 0,
    Building = 1,
    Starting = 2,
    Executing = 3,
    Finished = 4,
}

/// <summary>See <see cref="PipelineExecutionState"/>.</summary>
internal enum PipelineExecutionResult
{
    None = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
}
