import { HttpErrorResponse } from '@angular/common/http';

/** The API reports failures as RFC 7807 problem details produced by the NumoResult filter. */
export function readProblemDetail(error: HttpErrorResponse): string {
  return error.error?.detail ?? error.error?.title ?? error.message;
}

/**
 * The stable ids the ServiceData slice declares in ServiceDataErrors.cs. Every failure of that slice
 * arrives as HTTP 400, because that is how numo-core maps a failed NumoResult, so a caller that needs
 * to tell "no such record" from a validation problem has to branch on the id rather than the status.
 */
export const SERVICE_DATA_ERROR_IDS = {
  unknownResource: '6f1b9c2e-4d83-4a17-9b5c-2e7a8d40f913',
  tenantIdMissing: 'b0c74e15-93af-4d26-8f31-5a6b2c9e7d04',
  recordNotFound: 'd38f5a62-7c14-4e9b-86d0-3f1b7e2a95c8',
  downstreamCallFailed: '1e5a9d37-2b6c-4f80-9d14-7c3e8b5f2a60',
  downstreamCallUnauthorized: '47c2e8b9-5f01-4a3d-92b6-8d7e1c4a06f5',
} as const;

/** The error id a NumoResult failure carries, lower-cased so a comparison cannot miss on casing. */
export function readProblemErrorId(error: HttpErrorResponse): string | null {
  const errorId: unknown = error.error?.errorId;

  return typeof errorId === 'string' ? errorId.toLowerCase() : null;
}
