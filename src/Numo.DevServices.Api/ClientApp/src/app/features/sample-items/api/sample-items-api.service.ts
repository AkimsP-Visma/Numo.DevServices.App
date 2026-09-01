import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import { PagedRequest, PaginatedList, toPagedQueryParams } from '../../../shared/api/paginated-list.model';
import { CreateSampleItemRequest, CreateSampleItemResponse, SampleItem } from './sample-item.model';

@Injectable({ providedIn: 'root' })
export class SampleItemsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/sample-items`;

  getAll(pagedRequest: PagedRequest): Observable<PaginatedList<SampleItem>> {
    return this.httpClient.get<PaginatedList<SampleItem>>(this.baseUrl, {
      params: toPagedQueryParams(pagedRequest),
    });
  }

  getById(id: string): Observable<SampleItem> {
    return this.httpClient.get<SampleItem>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateSampleItemRequest): Observable<CreateSampleItemResponse> {
    return this.httpClient.post<CreateSampleItemResponse>(this.baseUrl, request);
  }
}
