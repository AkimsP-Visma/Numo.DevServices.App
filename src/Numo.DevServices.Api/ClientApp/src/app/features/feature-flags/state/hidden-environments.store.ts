import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'devservices.feature-flags.hidden-environments';

/**
 * Which environment columns the reader has hidden - stored as the hidden set, not the visible one,
 * so an environment added later starts visible rather than silently missing until the reader
 * notices and opts back in. Persisted in localStorage, guarded the same way as TenantIdStore: a
 * private window or blocked site data makes the accessor itself throw rather than return null.
 */
@Injectable({ providedIn: 'root' })
export class HiddenEnvironmentsStore {
  private readonly current = signal(readStoredHiddenKeys());

  readonly hiddenKeys = this.current.asReadonly();

  isHidden(environmentKey: string): boolean {
    return this.current().has(environmentKey);
  }

  toggle(environmentKey: string): void {
    const next = new Set(this.current());

    if (next.has(environmentKey)) {
      next.delete(environmentKey);
    } else {
      next.add(environmentKey);
    }

    this.current.set(next);
    persist(next);
  }
}

function readStoredHiddenKeys(): ReadonlySet<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);

    return raw ? new Set(JSON.parse(raw)) : new Set();
  } catch {
    return new Set();
  }
}

function persist(keys: ReadonlySet<string>): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify([...keys]));
  } catch {
    // The choice still works for this session; only persistence is lost.
  }
}
