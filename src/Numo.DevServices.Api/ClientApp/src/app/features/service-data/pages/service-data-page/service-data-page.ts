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
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import {
  readProblemDetail,
  readProblemErrorId,
  SERVICE_DATA_ERROR_IDS,
} from '../../../../shared/api/problem-details';
import { ResourceDescriptor, ResourcePage } from '../../api/service-data.model';
import { ServiceDataApiService } from '../../api/service-data-api.service';
import {
  FilterValues,
  ResourceFilterBar,
} from '../../components/resource-filter-bar/resource-filter-bar';
import {
  GenericRecordTable,
  SortRequest,
} from '../../components/generic-record-table/generic-record-table';
import { TenantIdField } from '../../components/tenant-id-field/tenant-id-field';
import { TenantIdStore } from '../../state/tenant-id.store';

const FIRST_PAGE = 1;

/** Reserved query-string keys, so everything else in the URL is a declared filter value. */
const PAGE_PARAM = 'page';
const SORT_PARAM = 'sort';

/** Which nav entry (Personnel Browser / DataIntegration Browser) linked here, used only to
 * preselect the picker - not a filter, and preserved across navigation via queryParamsHandling. */
const SECTION_PARAM = 'section';

@Component({
  selector: 'app-service-data-page',
  imports: [ButtonModule, MessageModule, GenericRecordTable, ResourceFilterBar, TenantIdField],
  templateUrl: './service-data-page.html',
  styleUrl: './service-data-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceDataPage {
  private readonly serviceDataApi = inject(ServiceDataApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly tenantIdStore = inject(TenantIdStore);

  protected readonly resources = signal<readonly ResourceDescriptor[]>([]);
  protected readonly page = signal<ResourcePage | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly catalogueError = signal<string | null>(null);

  protected readonly hasTenantId = this.tenantIdStore.hasTenantId;

  // The route is read as signals so a relation link, the picker and the back button all take the
  // same path: change the URL, and the page reloads from it.
  private readonly routeParams = toSignal(this.route.paramMap, { initialValue: null });
  private readonly queryParams = toSignal(this.route.queryParamMap, { initialValue: null });

  protected readonly resourceKey = computed(() => this.routeParams()?.get('resource') ?? null);

  protected readonly descriptor = computed(
    () => this.resources().find((resource) => resource.key === this.resourceKey()) ?? null,
  );

  protected readonly sectionParam = computed(() => this.queryParams()?.get(SECTION_PARAM) ?? null);

  /** Reachable-only-by-relation resources (connection credentials, certificates) never appear here:
   * picking them any other way would defeat the point of gating them behind a relation button. */
  protected readonly pickerResources = computed(() => {
    const section = this.sectionParam();

    return this.resources().filter(
      (resource) =>
        !resource.isReachableOnlyByRelation && (!section || resource.section === section),
    );
  });

  protected readonly sectionTitle = computed(() => {
    switch (this.sectionParam()) {
      case 'Personnel':
        return 'Personnel Browser';
      case 'DataIntegration':
        return 'DataIntegration Browser';
      default:
        return 'Service data';
    }
  });

  /** DataIntegration resources need no tenant at all; defaults true so a slow-loading catalogue
   * does not briefly let an unrelated request through before the descriptor arrives. */
  protected readonly requiresTenant = computed(() => this.descriptor()?.requiresTenant ?? true);

  /** A nested resource (di-client-resources and its siblings) cannot be browsed until its parent id
   * is set, which happens only by following a relation link - never by typing into this page. */
  protected readonly missingRequiredFilter = computed(() =>
    this.descriptor()?.filters.find((filter) => filter.isRequired && !this.filterValues()[filter.key]),
  );

  protected readonly currentPage = computed(() => {
    const raw = Number(this.queryParams()?.get(PAGE_PARAM) ?? FIRST_PAGE);

    return Number.isFinite(raw) && raw >= FIRST_PAGE ? Math.trunc(raw) : FIRST_PAGE;
  });

  /** The downstream form too: a leading minus is descending, which keeps one spelling in play. */
  private readonly sortParam = computed(() => this.queryParams()?.get(SORT_PARAM) ?? '');

  protected readonly sortColumn = computed(() => {
    const sort = this.sortParam();

    return sort.length === 0 ? null : sort.replace(/^-/, '');
  });

  protected readonly isSortDescending = computed(() => this.sortParam().startsWith('-'));

  protected readonly filterValues = computed<FilterValues>(() => {
    const params = this.queryParams();
    const descriptor = this.descriptor();
    const values: FilterValues = {};

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
    this.loadCatalogue();

    effect(() => this.loadPage());
  }

  protected selectResource(key: string): void {
    // Filters and page belong to the resource that declared them, so they do not survive a switch.
    // section is preserved via merge: it is not a filter, it is which picker list is showing.
    this.router.navigate(['/service-data', key], { queryParamsHandling: 'merge' });
  }

  protected applyFilters(values: FilterValues): void {
    this.navigateWith(values, FIRST_PAGE, this.sortParam());
  }

  protected applySort(request: SortRequest): void {
    const sort = `${request.isDescending ? '-' : ''}${request.column}`;

    this.navigateWith(this.filterValues(), FIRST_PAGE, sort);
  }

  protected goToPage(page: number): void {
    this.navigateWith(this.filterValues(), page, this.sortParam());
  }

  private navigateWith(values: FilterValues, page: number, sort: string): void {
    const queryParams: Record<string, string | number | null> = {
      [PAGE_PARAM]: page === FIRST_PAGE ? null : page,
      [SORT_PARAM]: sort.length === 0 ? null : sort,
    };

    for (const filter of this.descriptor()?.filters ?? []) {
      const value = values[filter.key] ?? '';

      queryParams[filter.key] = value.length === 0 ? null : value;
    }

    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  private loadCatalogue(): void {
    this.serviceDataApi.getCatalogue().subscribe({
      next: (catalogue) => this.resources.set(catalogue.resources),
      error: (error: HttpErrorResponse) => this.catalogueError.set(readProblemDetail(error)),
    });
  }

  private loadPage(): void {
    const resourceKey = this.resourceKey();
    const descriptor = this.descriptor();
    const filters = this.filterValues();
    const page = this.currentPage();
    const sortColumn = this.sortColumn();
    const isSortDescending = this.isSortDescending();

    // No tenant means no request at all: the services answer an empty list for an unknown tenant,
    // which would read as "no data" rather than "nothing asked yet". DataIntegration resources
    // declare requiresTenant false and skip this gate entirely.
    if (!resourceKey || !descriptor || (this.requiresTenant() && !this.tenantIdStore.hasTenantId())) {
      this.page.set(null);
      return;
    }

    // A nested resource cannot be browsed until its required filter (a parent id) is set, which
    // only happens by following a relation link - firing the request anyway would just 400.
    if (this.missingRequiredFilter()) {
      this.page.set(null);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.serviceDataApi
      .getPage(resourceKey, { page, pageSize: 20, sortColumn, isSortDescending, filters })
      .subscribe({
        next: (loaded) => {
          this.page.set(loaded);
          this.isLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.page.set(null);
          this.isLoading.set(false);
          this.errorMessage.set(describeFailure(error));
        },
      });
  }
}

/**
 * Every failure of the slice arrives as HTTP 400, so the error id is the only way to tell them apart.
 */
function describeFailure(error: HttpErrorResponse): string {
  switch (readProblemErrorId(error)) {
    case SERVICE_DATA_ERROR_IDS.tenantIdMissing:
      return 'That tenant id is not a GUID. Correct it and the page will reload.';
    case SERVICE_DATA_ERROR_IDS.unknownResource:
      return 'No such resource. Pick one from the list.';
    case SERVICE_DATA_ERROR_IDS.downstreamCallUnauthorized:
      return 'The service rejected the call before it was sent, because this app has no authenticated principal. Check that FeatureManagement:AllowUnauthorizedApiCalls is set.';
    default:
      return readProblemDetail(error);
  }
}
