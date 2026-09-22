using Numo.Employee.Lib.Clients.JobTitle;
using Numo.Employee.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Employee service's job titles, and the smallest resource in the slice: <c>JobTitleDto</c> is
/// <c>id, name, deletedAt</c> and nothing else, so there is one column and no relation to declare -
/// no filter in the catalogue can select by a job title, positions included.
/// </summary>
public sealed class JobTitlesResource(IJobTitleClient jobTitleClient) : IServiceDataResource
{
    private const string ResourceKey = "job-titles";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    // Only a column probed to actually sort is marked sortable: the service silently ignores an
    // order on a column it does not mark orderable, which would show as a sort that does nothing.
    // Name sorts. Id does not, so it is not a column here at all.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Job titles",
        "Numo.Employee.Api",
        ResourceSection.Personnel,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: true),
        ],
        [
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, page, pageSize),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var jobTitle = await DownstreamCall.FindResultAsync(
            () => jobTitleClient.GetJobTitle(id),
            id,
            $"{ResourceKey} record");

        return jobTitle is null ? null : ToRecord(jobTitle);
    }

    private Task<IEnumerable<JobTitleDto>> FetchPageAsync(ResourceQuery query, int page, int pageSize)
    {
        // Page and PageSize are always set: an unset filter is a full-table read.
        var filter = new JobTitleFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = EmployeeOrderBy.From(query),
            IncludeDeletedSince = ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey),
        };

        return DownstreamCall.InvokeResultAsync(
            () => jobTitleClient.GetJobTitles(filter),
            $"{ResourceKey} page {page}");
    }

    // Cells are positional: this order is the Descriptor.Columns order.
    private static ResourceRow ToRow(JobTitleDto jobTitle)
        => new(jobTitle.Id, jobTitle.DeletedAt, [new Cell(jobTitle.Name, Link: null)]);

    private static ResourceRecord ToRecord(JobTitleDto jobTitle)
        => new(
            jobTitle.Id,
            string.IsNullOrWhiteSpace(jobTitle.Name) ? jobTitle.Id.ToString() : jobTitle.Name,
            [
                new FieldValue("Id", jobTitle.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", jobTitle.Name, FieldKind.Text, Link: null),
                new FieldValue("Deleted at", FieldFormat.Format(jobTitle.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            []);
}
