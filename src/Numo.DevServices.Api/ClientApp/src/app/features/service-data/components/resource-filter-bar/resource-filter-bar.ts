import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { FilterDescriptor } from '../../api/service-data.model';

export type FilterValues = Record<string, string>;

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

const BOOLEAN_OPTIONS: readonly SelectOption[] = [
  { label: 'Any', value: '' },
  { label: 'Yes', value: 'true' },
  { label: 'No', value: 'false' },
];

/**
 * One input per declared filter, typed by its kind. Separate from the page because the widget set
 * grows with FilterKind, and a GuidList needs a hint about its cap that the other kinds do not.
 */
@Component({
  selector: 'app-resource-filter-bar',
  imports: [ButtonModule, DatePickerModule, FormsModule, InputTextModule, SelectModule],
  templateUrl: './resource-filter-bar.html',
  styleUrl: './resource-filter-bar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceFilterBar {
  readonly filters = input.required<readonly FilterDescriptor[]>();
  readonly values = input.required<FilterValues>();

  readonly applied = output<FilterValues>();

  protected readonly draft = signal<FilterValues>({});
  protected readonly booleanOptions = BOOLEAN_OPTIONS;

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

  protected enumOptions(filter: FilterDescriptor): readonly SelectOption[] {
    const options = (filter.options ?? []).map((option) => ({ label: option, value: option }));

    return [{ label: 'Any', value: '' }, ...options];
  }

  /** p-datepicker binds a Date, but a filter value is the plain yyyy-MM-dd string the backend
   * expects - parsed/formatted in local time so the picker never drifts a day at a UTC offset. */
  protected dateValueOf(key: string): Date | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(this.valueOf(key));

    return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
  }

  protected setDateValue(key: string, value: Date | null): void {
    if (!value) {
      this.setValue(key, '');
      return;
    }

    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');

    this.setValue(key, `${year}-${month}-${day}`);
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
