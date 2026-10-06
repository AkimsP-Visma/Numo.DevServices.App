/** Mirrors the backend's batch cap, so an oversized batch is caught before it is sent. */
export const MAX_BATCH_SIZE = 1000;

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const TAB = '\t';
const COMMA = ',';
const WHITESPACE_RUN = /\s+/;

export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value.trim());
}

/** One key per non-blank line; blank lines are skipped rather than sent. */
export interface ParsedKeyLines<TKey> {
  readonly keys: readonly TKey[];
  /** 1-based, counted over the non-blank lines only. */
  readonly invalidLineNumbers: readonly number[];
}

export function parseNumoKeys(text: string): ParsedKeyLines<string> {
  const keys = splitLines(text);

  return { keys, invalidLineNumbers: findInvalidLineNumbers(keys, isGuid) };
}

/**
 * One connector key per line, its values in key-field order. A single-field key is the whole line,
 * so a value containing spaces or commas survives. A multi-field key splits on tabs when the line
 * has one (a spreadsheet copy), else on commas, else on runs of spaces; a line that yields the
 * wrong number of values is flagged rather than guessed at, so no value lands in the wrong field.
 */
export function parseConnectorKeys(text: string, keyFieldCount: number): ParsedKeyLines<string[]> {
  const keys = splitLines(text).map((line) => splitValues(line, keyFieldCount));

  return {
    keys,
    invalidLineNumbers: findInvalidLineNumbers(
      keys,
      (values) => values.length === keyFieldCount && values.every((value) => value.length > 0),
    ),
  };
}

function splitValues(line: string, keyFieldCount: number): string[] {
  if (keyFieldCount === 1) {
    return [line];
  }

  const separator = line.includes(TAB) ? TAB : line.includes(COMMA) ? COMMA : WHITESPACE_RUN;

  return line.split(separator).map((value) => value.trim());
}

function findInvalidLineNumbers<TKey>(
  keys: readonly TKey[],
  isValid: (key: TKey) => boolean,
): number[] {
  return keys
    .map((key, index) => (isValid(key) ? null : index + 1))
    .filter((lineNumber): lineNumber is number => lineNumber !== null);
}

function splitLines(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}
