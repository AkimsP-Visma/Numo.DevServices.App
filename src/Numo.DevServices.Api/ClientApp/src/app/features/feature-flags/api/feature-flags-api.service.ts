import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import { FeatureFlagList } from './feature-flag.model';

@Injectable({ providedIn: 'root' })
export class FeatureFlagsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/feature-flags`;

  getAll(): Observable<FeatureFlagList> {
    return this.httpClient.get<FeatureFlagList>(this.baseUrl);
  }
}
