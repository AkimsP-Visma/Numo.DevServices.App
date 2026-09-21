import { HttpErrorResponse } from '@angular/common/http';

/** The API reports failures as RFC 7807 problem details produced by the NumoResult filter. */
export function readProblemDetail(error: HttpErrorResponse): string {
  return error.error?.detail ?? error.error?.title ?? error.message;
}
