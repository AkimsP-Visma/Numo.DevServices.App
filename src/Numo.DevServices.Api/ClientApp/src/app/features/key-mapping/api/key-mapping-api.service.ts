import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_PATH } from '../../../shared/api/api-paths';
import {
  KeyMappingConnector,
  KeyMappingOption,
  KeyMappingResult,
  KeyMappingTarget,
} from './key-mapping.model';

@Injectable({ providedIn: 'root' })
export class KeyMappingApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_PATH}/key-mappings/clients`;

  getClients(): Observable<KeyMappingOption[]> {
    return this.httpClient.get<KeyMappingOption[]>(this.baseUrl);
  }

  getResources(clientId: string): Observable<KeyMappingOption[]> {
    return this.httpClient.get<KeyMappingOption[]>(`${this.baseUrl}/${clientId}/resources`);
  }

  getConnectors(clientId: string, resourceId: string): Observable<KeyMappingConnector[]> {
    return this.httpClient.get<KeyMappingConnector[]>(
      `${this.baseUrl}/${clientId}/resources/${resourceId}/connectors`,
    );
  }

  convertToNumoKeys(
    target: KeyMappingTarget,
    connectorKeys: readonly (readonly string[])[],
  ): Observable<KeyMappingResult> {
    return this.httpClient.post<KeyMappingResult>(this.conversionUrl(target, 'numo-keys'), {
      connectorName: target.connectorName,
      organizationId: target.organizationId,
      connectorKeys,
    });
  }

  convertToConnectorKeys(
    target: KeyMappingTarget,
    numoKeys: readonly string[],
  ): Observable<KeyMappingResult> {
    return this.httpClient.post<KeyMappingResult>(this.conversionUrl(target, 'connector-keys'), {
      connectorName: target.connectorName,
      organizationId: target.organizationId,
      numoKeys,
    });
  }

  private conversionUrl(target: KeyMappingTarget, route: string): string {
    return `${this.baseUrl}/${target.clientId}/resources/${target.resourceId}/${route}`;
  }
}
