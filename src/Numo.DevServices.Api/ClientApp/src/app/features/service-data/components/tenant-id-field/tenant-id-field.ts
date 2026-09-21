import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TenantIdStore } from '../../state/tenant-id.store';

/**
 * Where the tenant id comes from. Every service-data request carries it, and the services answer an
 * empty list for a well-formed tenant they do not recognise, so the page says which one is in use
 * rather than leaving an empty grid to be read as "no data".
 */
@Component({
  selector: 'app-tenant-id-field',
  imports: [ButtonModule],
  templateUrl: './tenant-id-field.html',
  styleUrl: './tenant-id-field.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantIdField {
  private readonly tenantIdStore = inject(TenantIdStore);

  protected readonly tenantId = this.tenantIdStore.tenantId;

  protected apply(value: string): void {
    this.tenantIdStore.set(value);
  }

  protected clear(): void {
    this.tenantIdStore.set('');
  }
}
