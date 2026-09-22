import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import { PageRequest, ResourceCatalogue, ResourcePage, ResourceRecord } from './service-data.model';
import { TenantIdStore } from '../state/tenant-id.store';

/** The platform's own header name, which the client libraries pass on to the services unchanged. */
const TENANT_ID_HEADER = 'Numo-Tenant-Id';

/**
 * The only place the tenant header is attached. A global interceptor would put it on the LaunchDarkly
 * and ping calls too, and this app has no per-feature interceptor mechanism.
 */
@Injectable({ providedIn: 'root' })
export class ServiceDataApiService {
  private readonly http = inject(HttpClient);
  private readonly tenantIdStore = inject(TenantIdStore);

  private readonly baseUrl = `${API_BASE_PATH}/service-data`;

  /** Compile-time metadata, so it needs no tenant and works before one has been typed in. */
  getCatalogue(): Observable<ResourceCatalogue> {
    return this.http.get<ResourceCatalogue>(this.baseUrl);
  }

  getPage(resource: string, request: PageRequest): Observable<ResourcePage> {
    let params = new HttpParams().set('page', request.page).set('pageSize', request.pageSize);

    if (request.sortColumn) {
      params = params
        .set('sortColumn', request.sortColumn)
        .set('isSortDescending', request.isSortDescending);
    }

    // The backend reads filters as filters[key]=value: a bare key is ignored rather than rejected.
    for (const [key, value] of Object.entries(request.filters)) {
      if (value.length > 0) {
        params = params.set(`filters[${key}]`, value);
      }
    }

    return this.http.get<ResourcePage>(this.resourceUrl(resource), {
      params,
      headers: this.tenantHeaders(),
    });
  }

  /** filters: most resources ignore them; a nested resource's detail route needs the parent id
   * one of them carries, so the record page forwards whatever filters were active on its grid. */
  getRecord(
    resource: string,
    id: string,
    filters: Readonly<Record<string, string>> = {},
  ): Observable<ResourceRecord> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(filters)) {
      if (value.length > 0) {
        params = params.set(`filters[${key}]`, value);
      }
    }

    return this.http.get<ResourceRecord>(`${this.resourceUrl(resource)}/${encodeURIComponent(id)}`, {
      params,
      headers: this.tenantHeaders(),
    });
  }

  private resourceUrl(resource: string): string {
    return `${this.baseUrl}/${encodeURIComponent(resource)}`;
  }

  private tenantHeaders(): HttpHeaders {
    return new HttpHeaders({ [TENANT_ID_HEADER]: this.tenantIdStore.tenantId() });
  }
}
