import { Injectable, inject, signal } from '@angular/core';
import { EnvironmentsApiService } from './environments-api.service';

/**
 * Which Numo environment (Testing/Staging/Production/Local) every backend call currently targets.
 * Loaded once at app start; switching calls the backend then reloads the page, so no component is
 * left holding data fetched under the old environment - simpler and safer than per-page
 * cache invalidation.
 */
@Injectable({ providedIn: 'root' })
export class EnvironmentStore {
  private readonly api = inject(EnvironmentsApiService);

  private readonly currentSignal = signal('');
  private readonly knownSignal = signal<readonly string[]>([]);

  readonly current = this.currentSignal.asReadonly();
  readonly known = this.knownSignal.asReadonly();

  constructor() {
    this.api.getCurrent().subscribe((loaded) => {
      this.currentSignal.set(loaded.current);
      this.knownSignal.set(loaded.known);
    });
  }

  switchTo(environmentKey: string): void {
    this.api.setCurrent(environmentKey).subscribe(() => {
      window.location.reload();
    });
  }
}
