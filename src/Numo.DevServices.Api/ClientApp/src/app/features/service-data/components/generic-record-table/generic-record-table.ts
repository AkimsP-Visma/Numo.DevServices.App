import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { formatFieldValue } from '../../api/format-field-value';
import { Cell, ColumnDescriptor, ResourceRow } from '../../api/service-data.model';

export interface SortRequest {
  readonly column: string;
  readonly isDescending: boolean;
}

/**
 * Renders whatever the descriptor declares, for every resource. A cell carrying a link becomes a
 * router link into that record, which is what lets grid-to-grid navigation need no per-resource code.
 */
@Component({
  selector: 'app-generic-record-table',
  imports: [RouterLink, TableModule, TagModule],
  templateUrl: './generic-record-table.html',
  styleUrl: './generic-record-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GenericRecordTable {
  readonly resourceKey = input.required<string>();
  readonly columns = input.required<readonly ColumnDescriptor[]>();
  readonly rows = input.required<readonly ResourceRow[]>();
  readonly sortColumn = input<string | null>(null);
  readonly isSortDescending = input(false);

  /** Carried on the Open link only, not on cross-resource cell links: a nested resource's own
   * detail route needs the parent id one of these carries, and a cell link only ever points at a
   * flat resource (see the backend's ResourceDescriptor/RecordLink convention). */
  readonly filters = input<Readonly<Record<string, string>>>({});

  readonly sortRequested = output<SortRequest>();

  /** [customSort] stops p-table from sorting `rows()` itself against `column.key` - our rows carry
   * cells positionally, not as flat properties the table could resolve - so this only forwards the
   * click's intent to the parent, which re-fetches the page already sorted by the server. */
  protected onSort(event: { field: string; order: number }): void {
    this.sortRequested.emit({ column: event.field, isDescending: event.order === -1 });
  }

  protected display(cell: Cell, column: ColumnDescriptor): string {
    return formatFieldValue(cell.value, column.kind);
  }
}
