import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import { NumoService } from './numo-service.model';

@Injectable({ providedIn: 'root' })
export class ServicesApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/services`;

  getAll(): Observable<NumoService[]> {
    return this.httpClient.get<NumoService[]>(this.baseUrl);
  }

  /** Swagger UI fetches the document itself, so it needs the URL rather than the parsed spec. */
  getOpenApiUrl(serviceName: string): string {
    return `${this.baseUrl}/${encodeURIComponent(serviceName)}/openapi`;
  }
}
