import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { KeyMappingApiService } from '../../api/key-mapping-api.service';
import {
  KeyMappingConnector,
  KeyMappingOption,
  KeyMappingPick,
} from '../../api/key-mapping.model';

const CLIENT_ID_PARAM = 'clientId';
const RESOURCE_ID_PARAM = 'resourceId';
const CONNECTOR_PARAM = 'connector';

interface ConnectorOption {
  readonly label: string;
  readonly name: string | null;
  readonly isDisabled: boolean;
}

/**
 * Client, resource and connector, kept in the query string so a URL reproduces the target. The
 * organization id is not chosen here: it is the app-wide tenant id. Each list loads only once its parent is chosen, and a response that arrives after the
 * parent changed again is dropped.
 */
@Component({
  selector: 'app-key-mapping-target-picker',
  imports: [FormsModule, MessageModule, SelectModule],
  templateUrl: './key-mapping-target-picker.html',
  styleUrl: './key-mapping-target-picker.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyMappingTargetPicker {
  private readonly keyMappingApi = inject(KeyMappingApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /** Null until client, resource and a named connector are all set. */
  readonly pickChange = output<KeyMappingPick | null>();

  protected readonly clients = signal<KeyMappingOption[]>([]);
  protected readonly resources = signal<KeyMappingOption[]>([]);
  /** Null until loaded, so an empty list can be told apart from one still on its way. */
  protected readonly connectors = signal<KeyMappingConnector[] | null>(null);

  protected readonly clientId = signal<string | null>(null);
  protected readonly resourceId = signal<string | null>(null);
  protected readonly connectorName = signal<string | null>(null);

  protected readonly errorMessage = signal<string | null>(null);

  protected readonly connectorOptions = computed<ConnectorOption[]>(() =>
    (this.connectors() ?? []).map((connector) => ({
      label: connector.name ?? `${connector.connectorId} (no name, cannot convert)`,
      name: connector.name,
      isDisabled: connector.name === null,
    })),
  );

  protected readonly keyFieldsLabel = computed(
    () => this.selectedConnector()?.keyFields.join(', ') ?? null,
  );

  private readonly selectedConnector = computed(
    () => this.connectors()?.find((connector) => connector.name === this.connectorName()) ?? null,
  );

  constructor() {
    const params = this.route.snapshot.queryParamMap;

    this.loadClients(
      params.get(CLIENT_ID_PARAM),
      params.get(RESOURCE_ID_PARAM),
      params.get(CONNECTOR_PARAM),
    );
  }

  protected onClientSelected(clientId: string | null): void {
    this.clientId.set(clientId);
    this.clearBelowClient();
    this.publish();

    if (clientId) {
      this.loadResources(clientId, null, null);
    }
  }

  protected onResourceSelected(resourceId: string | null): void {
    this.resourceId.set(resourceId);
    this.clearBelowResource();
    this.publish();

    const clientId = this.clientId();

    if (clientId && resourceId) {
      this.loadConnectors(clientId, resourceId, null);
    }
  }

  protected onConnectorSelected(connectorName: string | null): void {
    this.connectorName.set(connectorName);
    this.publish();
  }

  private loadClients(
    pendingClientId: string | null,
    pendingResourceId: string | null,
    pendingConnector: string | null,
  ): void {
    this.keyMappingApi.getClients().subscribe({
      next: (clients) => {
        this.clients.set(clients);

        if (pendingClientId && clients.some((client) => client.id === pendingClientId)) {
          this.clientId.set(pendingClientId);
          this.loadResources(pendingClientId, pendingResourceId, pendingConnector);
        }

        this.publish();
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(readProblemDetail(error)),
    });
  }

  private loadResources(
    clientId: string,
    pendingResourceId: string | null,
    pendingConnector: string | null,
  ): void {
    this.errorMessage.set(null);

    this.keyMappingApi.getResources(clientId).subscribe({
      next: (resources) => {
        if (this.clientId() !== clientId) {
          return;
        }

        this.resources.set(resources);

        if (pendingResourceId && resources.some((resource) => resource.id === pendingResourceId)) {
          this.resourceId.set(pendingResourceId);
          this.loadConnectors(clientId, pendingResourceId, pendingConnector);
        }

        this.publish();
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(readProblemDetail(error)),
    });
  }

  private loadConnectors(
    clientId: string,
    resourceId: string,
    pendingConnector: string | null,
  ): void {
    this.errorMessage.set(null);

    this.keyMappingApi.getConnectors(clientId, resourceId).subscribe({
      next: (connectors) => {
        if (this.clientId() !== clientId || this.resourceId() !== resourceId) {
          return;
        }

        this.connectors.set(connectors);
        this.connectorName.set(this.chooseConnector(connectors, pendingConnector));
        this.publish();
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(readProblemDetail(error)),
    });
  }

  /** The linked connector when it exists, otherwise the only one when there is exactly one. */
  private chooseConnector(
    connectors: readonly KeyMappingConnector[],
    pendingConnector: string | null,
  ): string | null {
    if (pendingConnector && connectors.some((connector) => connector.name === pendingConnector)) {
      return pendingConnector;
    }

    return connectors.length === 1 ? connectors[0].name : null;
  }

  private clearBelowClient(): void {
    this.resources.set([]);
    this.resourceId.set(null);
    this.clearBelowResource();
  }

  private clearBelowResource(): void {
    this.connectors.set(null);
    this.connectorName.set(null);
  }

  private publish(): void {
    this.pickChange.emit(this.buildPick());

    void this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        [CLIENT_ID_PARAM]: this.clientId(),
        [RESOURCE_ID_PARAM]: this.resourceId(),
        [CONNECTOR_PARAM]: this.connectorName(),
      },
    });
  }

  private buildPick(): KeyMappingPick | null {
    const clientId = this.clientId();
    const resourceId = this.resourceId();
    const connector = this.selectedConnector();

    if (!clientId || !resourceId || !connector?.name) {
      return null;
    }

    return { clientId, resourceId, connectorName: connector.name, keyFields: connector.keyFields };
  }
}
