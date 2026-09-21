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
import { ResourceRecord } from '../../api/service-data.model';
import { ServiceDataApiService } from '../../api/service-data-api.service';
import { GenericRecordDetail } from '../../components/generic-record-detail/generic-record-detail';
import { TenantIdField } from '../../components/tenant-id-field/tenant-id-field';
import { TenantIdStore } from '../../state/tenant-id.store';

@Component({
  selector: 'app-service-data-record-page',
  imports: [RouterLink, MessageModule, GenericRecordDetail, TenantIdField],
  templateUrl: './service-data-record-page.html',
  styleUrl: './service-data-record-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceDataRecordPage {
  private readonly serviceDataApi = inject(ServiceDataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly tenantIdStore = inject(TenantIdStore);

  protected readonly record = signal<ResourceRecord | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isMissing = signal(false);

  protected readonly hasTenantId = this.tenantIdStore.hasTenantId;

  private readonly routeParams = toSignal(this.route.paramMap, { initialValue: null });

  protected readonly resourceKey = computed(() => this.routeParams()?.get('resource') ?? null);
  protected readonly recordId = computed(() => this.routeParams()?.get('id') ?? null);

  constructor() {
    effect(() => this.load());
  }

  private load(): void {
    const resourceKey = this.resourceKey();
    const id = this.recordId();

    if (!resourceKey || !id || !this.tenantIdStore.hasTenantId()) {
      this.record.set(null);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.isMissing.set(false);

    this.serviceDataApi.getRecord(resourceKey, id).subscribe({
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
