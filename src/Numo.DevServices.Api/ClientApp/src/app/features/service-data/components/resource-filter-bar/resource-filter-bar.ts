import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { FilterDescriptor } from '../../api/service-data.model';

export type FilterValues = Record<string, string>;

/**
 * One input per declared filter, typed by its kind. Separate from the page because the widget set
 * grows with FilterKind, and a GuidList needs a hint about its cap that the other kinds do not.
 */
@Component({
  selector: 'app-resource-filter-bar',
  imports: [ButtonModule],
  templateUrl: './resource-filter-bar.html',
  styleUrl: './resource-filter-bar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceFilterBar {
  readonly filters = input.required<readonly FilterDescriptor[]>();
  readonly values = input.required<FilterValues>();

  readonly applied = output<FilterValues>();

  protected readonly draft = signal<FilterValues>({});

  constructor() {
    // The URL is the source of truth, so a back navigation or a relation link refills the inputs.
    effect(() => this.draft.set({ ...this.values() }));
  }

  protected valueOf(key: string): string {
    return this.draft()[key] ?? '';
  }

  protected setValue(key: string, value: string): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  protected apply(): void {
    this.applied.emit({ ...this.draft() });
  }

  protected clear(): void {
    const cleared: FilterValues = {};

    for (const filter of this.filters()) {
      cleared[filter.key] = '';
    }

    this.draft.set(cleared);
    this.applied.emit(cleared);
  }
}
