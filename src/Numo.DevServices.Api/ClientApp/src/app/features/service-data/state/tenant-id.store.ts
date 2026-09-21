import { computed, Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'devservices.service-data.tenant-id';

/**
 * The tenant id every service-data request carries. It lives in localStorage so it survives a reload,
 * and every access is guarded because a private window or blocked site data makes the accessor itself
 * throw rather than return null.
 */
@Injectable({ providedIn: 'root' })
export class TenantIdStore {
  private readonly current = signal(readStoredTenantId());

  readonly tenantId = this.current.asReadonly();

  readonly hasTenantId = computed(() => this.current().length > 0);

  set(tenantId: string): void {
    const trimmed = tenantId.trim();
    this.current.set(trimmed);

    try {
      if (trimmed.length > 0) {
        localStorage.setItem(STORAGE_KEY, trimmed);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // The value still works for this session; only persistence is lost.
    }
  }
}

function readStoredTenantId(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}
