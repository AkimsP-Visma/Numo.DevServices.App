import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { FeatureFlag, FeatureFlagEnvironmentState } from '../../api/feature-flag.model';
import { FeatureFlagsApiService } from '../../api/feature-flags-api.service';

const ROLLOUT_LABEL = 'percentage rollout';
const SDK_DEFAULT_LABEL = 'SDK default';
const NOT_PRESENT_LABEL = '-';

@Component({
  selector: 'app-feature-flags-page',
  imports: [ButtonModule, MessageModule, TableModule, TagModule],
  templateUrl: './feature-flags-page.html',
  styleUrl: './feature-flags-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeatureFlagsPage {
  private readonly featureFlagsApi = inject(FeatureFlagsApiService);

  protected readonly flags = signal<FeatureFlag[]>([]);
  protected readonly environmentKeys = signal<string[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly columnCount = computed(() => this.environmentKeys().length + 2);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.featureFlagsApi.getAll().subscribe({
      next: (list) => {
        this.environmentKeys.set([...list.environmentKeys]);
        this.flags.set([...list.flags]);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }

  protected describeValue(state: FeatureFlagEnvironmentState | undefined): string {
    if (!state) {
      return NOT_PRESENT_LABEL;
    }

    if (state.isRollout) {
      return ROLLOUT_LABEL;
    }

    if (state.value === null || state.value === undefined) {
      return SDK_DEFAULT_LABEL;
    }

    return typeof state.value === 'string' ? state.value : JSON.stringify(state.value);
  }
}
