import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FieldValue } from '../../api/service-data.model';

/**
 * Every field the record carries. Relations render separately, as
 * app-related-records-panel - not here, since they fetch their own data and this stays a
 * pure display component.
 */
@Component({
  selector: 'app-generic-record-detail',
  imports: [RouterLink],
  templateUrl: './generic-record-detail.html',
  styleUrl: './generic-record-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GenericRecordDetail {
  readonly fields = input.required<readonly FieldValue[]>();

  protected display(field: FieldValue): string {
    return field.value === null || field.value.length === 0 ? '-' : field.value;
  }
}
