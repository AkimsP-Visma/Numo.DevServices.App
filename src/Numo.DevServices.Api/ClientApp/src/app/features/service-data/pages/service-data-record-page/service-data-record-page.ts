import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MessageModule } from 'primeng/message';
import {
  readProblemDetail,
  readProblemErrorId,
  SERVICE_DATA_ERROR_IDS,
} from '../../../../shared/api/problem-details';
import { ResourceDescriptor, ResourceRecord } from '../../api/service-data.model';
import { ServiceDataApiService } from '../../api/service-data-api.service';
import { GenericRecordDetail } from '../../components/generic-record-detail/generic-record-detail';
import { RelatedRecordsPanel } from '../../components/related-records-panel/related-records-panel';
import { TenantIdField } from '../../components/tenant-id-field/tenant-id-field';
import { TenantIdStore } from '../../state/tenant-id.store';

@Component({
  selector: 'app-service-data-record-page',
  imports: [RouterLink, MessageModule, GenericRecordDetail, RelatedRecordsPanel, TenantIdField],
  templateUrl: './service-data-record-page.html',
  styleUrl: './service-data-record-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceDataRecordPage {
  private readonly serviceDataApi = inject(ServiceDataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly tenantIdStore = inject(TenantIdStore);

  protected readonly resources = signal<readonly ResourceDescriptor[]>([]);
  protected readonly record = signal<ResourceRecord | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isMissing = signal(false);

  protected readonly hasTenantId = this.tenantIdStore.hasTenantId;

  private readonly routeParams = toSignal(this.route.paramMap, { initialValue: null });
  private readonly queryParams = toSignal(this.route.queryParamMap, { initialValue: null });

  protected readonly resourceKey = computed(() => this.routeParams()?.get('resource') ?? null);
  protected readonly recordId = computed(() => this.routeParams()?.get('id') ?? null);

  protected readonly descriptor = computed(
    () => this.resources().find((resource) => resource.key === this.resourceKey()) ?? null,
  );

  /** DataIntegration resources need no tenant; defaults true so a slow catalogue load does not
   * briefly let an unrelated request through. */
  protected readonly requiresTenant = computed(() => this.descriptor()?.requiresTenant ?? true);

  /** Only the filters this resource actually declared - a nested resource's parent-id filter -
   * survive into the request. Everything else in the URL (section, an unrelated leftover) is not
   * a filter this resource asked for and the backend would 400 on it. */
  private readonly filters = computed<Readonly<Record<string, string>>>(() => {
    const params = this.queryParams();
    const descriptor = this.descriptor();
    const values: Record<string, string> = {};

    if (!params || !descriptor) {
      return values;
    }

    for (const filter of descriptor.filters) {
      const value = params.get(filter.key);

      if (value) {
        values[filter.key] = value;
      }
    }

    return values;
  });

  constructor() {
    this.serviceDataApi.getCatalogue().subscribe({
      next: (catalogue) => this.resources.set(catalogue.resources),
      error: () => this.resources.set([]),
    });

    effect(() => this.load());
  }

  /** A relation names its target by key; the panel needs the target's descriptor (columns,
   * isReachableOnlyByRelation) to render and to decide whether it may load automatically. */
  protected descriptorFor(resourceKey: string): ResourceDescriptor | null {
    return this.resources().find((resource) => resource.key === resourceKey) ?? null;
  }

  private load(): void {
    const resourceKey = this.resourceKey();
    const id = this.recordId();
    const descriptor = this.descriptor();

    if (!resourceKey || !id || !descriptor || (this.requiresTenant() && !this.tenantIdStore.hasTenantId())) {
      this.record.set(null);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.isMissing.set(false);

    this.serviceDataApi.getRecord(resourceKey, id, this.filters()).subscribe({
      next: (loaded) => {
        this.record.set(loaded);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.record.set(null);
        this.isLoading.set(false);

        // A missing record and a validation failure are both HTTP 400 here, so the id decides which
        // of the two the page shows.
        if (readProblemErrorId(error) === SERVICE_DATA_ERROR_IDS.recordNotFound) {
          this.isMissing.set(true);
          return;
        }

        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }
}
