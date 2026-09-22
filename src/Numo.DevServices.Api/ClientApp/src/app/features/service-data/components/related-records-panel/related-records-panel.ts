import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { RelationDescriptor, ResourceDescriptor, ResourcePage } from '../../api/service-data.model';
import { ServiceDataApiService } from '../../api/service-data-api.service';
import { GenericRecordTable } from '../generic-record-table/generic-record-table';

/** Small enough to preview under a parent record; the "Open full list" link is where paging,
 * sorting and filtering the whole set actually happens. */
const PREVIEW_PAGE_SIZE = 10;

/**
 * One relation, embedded under its parent record instead of behind a nav tab that would open
 * empty and immediately demand the same filter this component already has.
 *
 * An ordinary relation loads as soon as its inputs resolve. A relation into a resource that
 * declares isReachableOnlyByRelation (connection credentials, certificates, an execution step's
 * dataset - secrets or personal data) stays behind a button instead: those must be fetched only
 * on demand, never speculatively on opening the parent record.
 */
@Component({
  selector: 'app-related-records-panel',
  imports: [RouterLink, ButtonModule, MessageModule, GenericRecordTable],
  templateUrl: './related-records-panel.html',
  styleUrl: './related-records-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RelatedRecordsPanel {
  private readonly serviceDataApi = inject(ServiceDataApiService);

  readonly relation = input.required<RelationDescriptor>();
  /** Resolved by the caller from the catalogue; null until the catalogue has loaded. */
  readonly descriptor = input.required<ResourceDescriptor | null>();

  protected readonly page = signal<ResourcePage | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isExpanded = signal(false);

  constructor() {
    effect(() => {
      const descriptor = this.descriptor();

      if (descriptor && !descriptor.isReachableOnlyByRelation) {
        this.load();
      }
    });
  }

  protected reveal(): void {
    this.isExpanded.set(true);
    this.load();
  }

  private load(): void {
    const relation = this.relation();
    const descriptor = this.descriptor();

    if (!descriptor) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.serviceDataApi
      .getPage(descriptor.key, {
        page: 1,
        pageSize: PREVIEW_PAGE_SIZE,
        sortColumn: null,
        isSortDescending: false,
        filters: relation.filters,
      })
      .subscribe({
        next: (loaded) => {
          this.page.set(loaded);
          this.isLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.isLoading.set(false);
          this.errorMessage.set(readProblemDetail(error));
        },
      });
  }
}
