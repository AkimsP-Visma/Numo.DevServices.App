import { FieldKind } from './service-data.model';

const DATE_TIME_FORMAT = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
});

/** The backend renders DateTime fields as round-trip ISO 8601 ("O" format) so the value stays
 * exactly parseable - correct for the wire, unreadable in a table. Reformatted for display only;
 * the underlying value (what gets copied, linked, etc.) is untouched. */
export function formatFieldValue(value: string | null, kind: FieldKind): string {
  if (value === null || value.length === 0) {
    return '-';
  }

  if (kind !== 'DateTime') {
    return value;
  }

  const parsed = new Date(value);

  return Number.isNaN(parsed.getTime()) ? value : DATE_TIME_FORMAT.format(parsed);
}
