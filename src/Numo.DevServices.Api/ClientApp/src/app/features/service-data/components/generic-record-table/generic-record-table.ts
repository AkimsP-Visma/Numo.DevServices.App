import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
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

  protected toggleSort(column: ColumnDescriptor): void {
    if (!column.isSortable) {
      return;
    }

    const isCurrent = this.sortColumn() === column.key;

    this.sortRequested.emit({
      column: column.key,
      isDescending: isCurrent ? !this.isSortDescending() : false,
    });
  }

  protected sortMarker(column: ColumnDescriptor): string {
    if (!column.isSortable || this.sortColumn() !== column.key) {
      return '';
    }

    return this.isSortDescending() ? ' (desc)' : ' (asc)';
  }

  /** Empty and absent read the same in a grid, so both show the placeholder rather than nothing. */
  protected display(cell: Cell): string {
    return cell.value === null || cell.value.length === 0 ? '-' : cell.value;
  }
}
