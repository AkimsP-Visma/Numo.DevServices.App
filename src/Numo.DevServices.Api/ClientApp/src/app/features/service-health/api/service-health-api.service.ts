import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import { ServiceHealthSnapshot } from './service-health.model';

@Injectable({ providedIn: 'root' })
export class ServiceHealthApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/service-health`;

  get(): Observable<ServiceHealthSnapshot> {
    return this.httpClient.get<ServiceHealthSnapshot>(this.baseUrl);
  }
}
