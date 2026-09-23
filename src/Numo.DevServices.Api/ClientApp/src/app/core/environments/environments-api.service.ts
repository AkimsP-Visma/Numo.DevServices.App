import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../shared/api/api-paths';

export interface CurrentEnvironment {
  readonly current: string;
  readonly known: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class EnvironmentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/environments`;

  getCurrent(): Observable<CurrentEnvironment> {
    return this.http.get<CurrentEnvironment>(this.baseUrl);
  }

  setCurrent(environmentKey: string): Observable<{ current: string }> {
    return this.http.put<{ current: string }>(`${this.baseUrl}/current`, { environmentKey });
  }
}
