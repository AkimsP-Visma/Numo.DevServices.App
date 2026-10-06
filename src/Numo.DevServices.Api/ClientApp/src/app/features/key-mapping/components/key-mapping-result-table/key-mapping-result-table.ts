import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { KeyMappingDirection, KeyMappingResult, KeyMappingRow } from '../../api/key-mapping.model';

const NUMO_KEY_HEADER = 'Numo key';
const TSV_CELL_SEPARATOR = '\t';
const TSV_LINE_SEPARATOR = '\n';

interface ResultLine {
  readonly isMapped: boolean;
  /** Input cells first, then output cells; null marks an output the service did not map. */
  readonly cells: readonly (string | null)[];
}

/** One line per submitted row, input columns on the left and the converted key on the right. */
@Component({
  selector: 'app-key-mapping-result-table',
  imports: [ButtonModule, MessageModule, TagModule],
  templateUrl: './key-mapping-result-table.html',
  styleUrl: './key-mapping-result-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyMappingResultTable {
  readonly result = input.required<KeyMappingResult>();
  readonly direction = input.required<KeyMappingDirection>();

  protected readonly copyStatus = signal<string | null>(null);

  protected readonly headers = computed(() =>
    this.direction() === 'toNumoKeys'
      ? [...this.result().keyFields, NUMO_KEY_HEADER]
      : [NUMO_KEY_HEADER, ...this.result().keyFields],
  );

  /** Where the output columns start, so only those get the "not found" marker. */
  protected readonly firstOutputColumn = computed(() =>
    this.direction() === 'toNumoKeys' ? this.result().keyFields.length : 1,
  );

  protected readonly lines = computed<ResultLine[]>(() =>
    this.result().rows.map((row) => ({ isMapped: row.isMapped, cells: this.toCells(row) })),
  );

  protected readonly mappedCount = computed(
    () => this.lines().filter((line) => line.isMapped).length,
  );

  protected async copyAsTsv(): Promise<void> {
    const text = [
      this.headers(),
      ...this.lines().map((line) => line.cells.map((cell) => cell ?? '')),
    ]
      .map((cells) => cells.join(TSV_CELL_SEPARATOR))
      .join(TSV_LINE_SEPARATOR);

    try {
      await navigator.clipboard.writeText(text);
      this.copyStatus.set(`Copied ${this.lines().length} rows.`);
    } catch (error) {
      console.error('Copying the key mapping result to the clipboard failed.', error);
      this.copyStatus.set('Copy failed: the browser denied clipboard access.');
    }
  }

  private toCells(row: KeyMappingRow): (string | null)[] {
    const keyValues = row.connectorKeyValues ?? this.result().keyFields.map(() => null);

    return this.direction() === 'toNumoKeys'
      ? [...keyValues, row.numoKey]
      : [row.numoKey, ...keyValues];
  }
}
