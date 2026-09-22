namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// One execution step's dataset - the actual records a pipeline moved, not configuration. This is
/// materially different from every other resource in the DataIntegration section: those carry no
/// personal data and need no gating, but a dataset's content is whatever the pipeline extracts, and
/// a live probe during this feature's build turned up an HR/absence pipeline whose dataset carried
/// real person-linked fields. <see cref="ResourceDescriptor.IsReachableOnlyByRelation"/> keeps this
/// out of the picker; the only way in is the "Dataset" relation on a
/// <see cref="ExecutionStepsResource"/> record, and the call is made only when that relation is
/// followed. See docs/Architecture.md for why this is a second, independent reason the slice needs
/// <c>[Authorize]</c>.
///
/// The route pages by continuation token, not by index - fundamentally different from every other
/// resource here, which is why this does not go through <see cref="ResourcePageBuilder"/>. There is
/// no way to jump to "page 3": a token is only good for "the batch after this one", and
/// <see cref="ResourceQuery"/> has nowhere to carry one across requests. So this resource fetches
/// one batch of <see cref="ResourceQuery.PageSize"/> records and stops - <see cref="ResourcePage.HasMore"/>
/// is always false, and <see cref="ResourcePage.Notice"/> says so when the service's own token shows
/// more exist, rather than the grid claiming a Next it cannot honour.
///
/// A record's own fields are whatever the source connector produced - there is no fixed schema, so
/// they cannot become <see cref="ColumnDescriptor"/>s (declared once for every row). The grid shows
/// only <c>state</c>; opening a record is where <see cref="ToRecord"/> renders every field the
/// record actually carries, because a <see cref="FieldValue"/> list is built per record already
/// and needs no static shape.
///
/// state (<see cref="DatasetRecord.State"/>) crosses the wire as a bare integer with no enum names
/// available anywhere reachable (the same situation as the other DataIntegration enums), so it
/// renders as <see cref="FieldKind.Number"/>.
///
/// There is no by-id route: <see cref="GetByIdAsync"/> re-fetches the same first batch
/// (deterministic - no continuation token is ever sent) and searches it, which finds exactly the
/// records the grid currently shows and nothing past them.
/// </summary>
public sealed class ExecutionStepDatasetResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-execution-step-dataset";
    private const string ExecutionIdFilterKey = "executionId";
    private const string StepIdFilterKey = "stepId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Execution step dataset",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("state", "State", FieldKind.Number, IsSortable: false),
        ],
        [
            new FilterDescriptor(ExecutionIdFilterKey, "Execution id", FilterKind.Guid, Options: null, IsRequired: true),
            new FilterDescriptor(StepIdFilterKey, "Step id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false,
        IsReachableOnlyByRelation: true);

    public async Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        var batch = await FetchBatchAsync(RequireExecutionId(query.Filters), RequireStepId(query.Filters), query.PageSize, cancellationToken);
        var records = batch.Dataset?.Data ?? [];

        var notice = batch.ContinuationToken is null
            ? null
            : $"Showing the first {records.Count} record(s). This dataset has more, but continuation-token paging is not supported here.";

        return new ResourcePage(records.Select(ToRow).ToList(), query.Page, query.PageSize, HasMore: false, notice);
    }

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var batch = await FetchBatchAsync(RequireExecutionId(filters), RequireStepId(filters), DefaultBatchSize, cancellationToken);
        var record = batch.Dataset?.Data?.FirstOrDefault(candidate => candidate.Id == id);

        return record is null ? null : ToRecord(record);
    }

    /// <summary>The page size a direct record fetch asks for - large enough to almost always
    /// contain a record the grid already showed, since the grid's own request used the caller's
    /// chosen page size and this one does not know it.</summary>
    private const int DefaultBatchSize = 200;

    private async Task<PipelineExecutionStepDatasetResponse> FetchBatchAsync(
        Guid executionId,
        Guid stepId,
        int pageSize,
        CancellationToken cancellationToken)
        => await api.GetAsync<PipelineExecutionStepDatasetResponse>(
            $"api/pipeline-executions/{executionId}/steps/{stepId}/dataset?pageSize={pageSize}",
            $"{ResourceKey} list",
            cancellationToken);

    private static Guid RequireExecutionId(IReadOnlyDictionary<string, string> filters)
        => RequireGuidFilter(filters, ExecutionIdFilterKey);

    private static Guid RequireStepId(IReadOnlyDictionary<string, string> filters)
        => RequireGuidFilter(filters, StepIdFilterKey);

    /// <summary>The page validator already rejects a request missing either filter before
    /// GetPageAsync runs; GetByIdAsync's caller does not, so this reports the same failure cleanly
    /// rather than throwing a bare exception that would surface as an unhandled 500.</summary>
    private static Guid RequireGuidFilter(IReadOnlyDictionary<string, string> filters, string filterKey)
        => filters.TryGetValue(filterKey, out var value) && Guid.TryParse(value, out var id)
            ? id
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, filterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid {filterKey} filter."));

    private static ResourceRow ToRow(DatasetRecord record)
        => new(record.Id, DeletedAt: null, [new Cell(FieldFormat.FormatNumber(record.State), Link: null)]);

    // Every key in Data becomes its own field: the schema is whatever the source connector
    // produced, so it cannot be declared once as a fixed set of columns the way every other
    // resource's grid is - but a record's own field list needs no such fixed shape.
    private static ResourceRecord ToRecord(DatasetRecord record)
        => new(
            record.Id,
            record.Id.ToString(),
            [
                new FieldValue("Id", record.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("State", FieldFormat.FormatNumber(record.State), FieldKind.Number, Link: null),
                .. (record.Data ?? new Dictionary<string, object?>())
                    .Select(field => new FieldValue(field.Key, FormatDataValue(field.Value), FieldKind.Text, Link: null)),
            ],
            []);

    private static string? FormatDataValue(object? value)
        => value?.ToString();
}

internal sealed record PipelineExecutionStepDatasetResponse(Dataset? Dataset, string? ContinuationToken);

internal sealed record Dataset(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<DatasetRecord>? Data);

internal sealed record DatasetRecord(Guid Id, int State, IReadOnlyDictionary<string, object?>? Data);
