export interface PaginatedList<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
}

export type SortDirection = 'Asc' | 'Desc';

export interface PagedRequest {
  readonly page: number;
  readonly limit: number;
  readonly direction: SortDirection;
}

/**
 * Numo.Core binds PagedDataRequest from nested query-string keys, so the flat request has to be
 * expanded into the shape the backend model binder expects.
 */
export function toPagedQueryParams(request: PagedRequest): Record<string, string> {
  return {
    'PagedRequest.Pagination.Page': String(request.page),
    'PagedRequest.Pagination.Limit': String(request.limit),
    'PagedRequest.Sorting.Direction': request.direction,
  };
}
