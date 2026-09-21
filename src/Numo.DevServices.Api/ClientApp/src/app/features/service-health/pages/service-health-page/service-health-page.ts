import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { timer } from 'rxjs';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { ServiceHealthApiService } from '../../api/service-health-api.service';
import { ServiceHealthStatus } from '../../api/service-health.model';

const REFRESH_INTERVAL_MS = 60_000;

@Component({
  selector: 'app-service-health-page',
  imports: [DatePipe, ButtonModule, MessageModule, TagModule],
  templateUrl: './service-health-page.html',
  styleUrl: './service-health-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceHealthPage {
  private readonly serviceHealthApi = inject(ServiceHealthApiService);

  protected readonly services = signal<ServiceHealthStatus[]>([]);
  protected readonly checkedAt = signal<Date | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly upCount = computed(
    () => this.services().filter((service) => service.isUp).length,
  );
  protected readonly downCount = computed(() => this.services().length - this.upCount());

  constructor() {
    timer(0, REFRESH_INTERVAL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  protected load(): void {
    // A ping round can outlive the interval when services hang; one in flight is enough.
    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.serviceHealthApi.get().subscribe({
      next: (snapshot) => {
        this.services.set([...snapshot.services]);
        this.checkedAt.set(new Date(snapshot.checkedAt));
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }
}
