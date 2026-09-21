using System.ComponentModel;
using Numo.Employee.Common.QuerySupport.Ordering;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The sort a query asks for, in the Employee service's own filter shape. Shared by every
/// Employee-family resource but not with the Person ones: each library declares its own
/// <c>NumoOrderByList</c>, so this cannot be one helper for the whole slice.
/// </summary>
internal static class EmployeeOrderBy
{
    /// <summary>An empty list is serialised as no OrderBy parameter at all, which is required: a
    /// blank <c>OrderBy=</c> is not a syntax either service accepts.</summary>
    public static NumoOrderByList From(ResourceQuery query)
    {
        var orderBy = new NumoOrderByList();

        if (!string.IsNullOrWhiteSpace(query.SortColumn))
        {
            orderBy.Add(
                query.SortColumn,
                query.IsSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending);
        }

        return orderBy;
    }
}
