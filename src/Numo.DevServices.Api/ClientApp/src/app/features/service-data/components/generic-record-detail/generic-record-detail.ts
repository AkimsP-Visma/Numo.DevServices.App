import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { FieldValue, RelationDescriptor } from '../../api/service-data.model';

/**
 * Every field the record carries, and a button per relation. A relation navigates to the list page
 * with its filter in the query string, so following one is an ordinary route change.
 */
@Component({
  selector: 'app-generic-record-detail',
  imports: [RouterLink, ButtonModule],
  templateUrl: './generic-record-detail.html',
  styleUrl: './generic-record-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GenericRecordDetail {
  readonly fields = input.required<readonly FieldValue[]>();
  readonly relations = input.required<readonly RelationDescriptor[]>();

  protected display(field: FieldValue): string {
    return field.value === null || field.value.length === 0 ? '-' : field.value;
  }

  protected relationQuery(relation: RelationDescriptor): Record<string, string> {
    return { [relation.filterKey]: relation.filterValue };
  }
}
