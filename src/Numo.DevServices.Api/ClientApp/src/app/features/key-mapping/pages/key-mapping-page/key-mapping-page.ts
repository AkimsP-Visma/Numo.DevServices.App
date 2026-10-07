import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TextareaModule } from 'primeng/textarea';
import { Observable } from 'rxjs';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { TenantIdField } from '../../../../shared/components/tenant-id-field/tenant-id-field';
import { TenantIdStore } from '../../../../shared/state/tenant-id.store';
import { KeyMappingApiService } from '../../api/key-mapping-api.service';
import {
  KeyMappingDirection,
  KeyMappingPick,
  KeyMappingResult,
  KeyMappingSelection,
} from '../../api/key-mapping.model';
import { KeyMappingResultTable } from '../../components/key-mapping-result-table/key-mapping-result-table';
import { KeyMappingTargetPicker } from '../../components/key-mapping-target-picker/key-mapping-target-picker';
import {
  isGuid,
  MAX_BATCH_SIZE,
  ParsedKeyLines,
  parseConnectorKeys,
  parseNumoKeys,
} from '../../input/key-input-parsing';

interface DirectionOption {
  readonly label: string;
  readonly value: KeyMappingDirection;
}

const DIRECTION_OPTIONS: readonly DirectionOption[] = [
  { label: 'Connector keys → Numo keys', value: 'toNumoKeys' },
  { label: 'Numo keys → Connector keys', value: 'toConnectorKeys' },
];

@Component({
  selector: 'app-key-mapping-page',
  imports: [
    FormsModule,
    ButtonModule,
    MessageModule,
    SelectButtonModule,
    TextareaModule,
    KeyMappingResultTable,
    KeyMappingTargetPicker,
    TenantIdField,
  ],
  templateUrl: './key-mapping-page.html',
  styleUrl: './key-mapping-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyMappingPage {
  private readonly keyMappingApi = inject(KeyMappingApiService);
  private readonly tenantIdStore = inject(TenantIdStore);

  protected readonly directionOptions = [...DIRECTION_OPTIONS];
  protected readonly maxBatchSize = MAX_BATCH_SIZE;

  private readonly pick = signal<KeyMappingPick | null>(null);

  protected readonly hasPick = computed(() => this.pick() !== null);

  /** The tenant id is the organization id the service filters its loaded dataset by. */
  protected readonly isTenantIdValid = computed(() => isGuid(this.tenantIdStore.tenantId()));

  protected readonly selection = computed<KeyMappingSelection | null>(() => {
    const pick = this.pick();

    if (!pick || !this.isTenantIdValid()) {
      return null;
    }

    const { keyFields, ...target } = pick;

    return { target: { ...target, organizationId: this.tenantIdStore.tenantId() }, keyFields };
  });
  protected readonly direction = signal<KeyMappingDirection>('toNumoKeys');
  /** Kept per direction, so switching back does not lose what was typed for the other one. */
  protected readonly connectorKeysText = signal('');
  protected readonly numoKeysText = signal('');

  protected readonly result = signal<KeyMappingResult | null>(null);
  /** The direction the shown result was produced in, which can differ from the toggle's now. */
  protected readonly resultDirection = signal<KeyMappingDirection>('toNumoKeys');
  protected readonly isConverting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly parsedConnectorKeys = computed(() =>
    parseConnectorKeys(this.connectorKeysText(), this.pick()?.keyFields.length ?? 1),
  );

  private readonly parsedNumoKeys = computed(() => parseNumoKeys(this.numoKeysText()));

  protected readonly parsedInput = computed<ParsedKeyLines<unknown>>(() =>
    this.direction() === 'toNumoKeys' ? this.parsedConnectorKeys() : this.parsedNumoKeys(),
  );

  protected readonly inputText = computed(() =>
    this.direction() === 'toNumoKeys' ? this.connectorKeysText() : this.numoKeysText(),
  );

  protected readonly inputLabel = computed(() => {
    const keyFields = this.pick()?.keyFields ?? [];

    if (this.direction() === 'toConnectorKeys') {
      return 'Numo keys, one per line';
    }

    return keyFields.length === 1
      ? `Connector keys (${keyFields[0]}), one per line`
      : `Connector keys, one per line: ${keyFields.join(', ')} - separated by tab, comma or space`;
  });

  protected readonly canConvert = computed(() => {
    const parsed = this.parsedInput();
    const count = parsed.keys.length;

    return (
      this.selection() !== null &&
      !this.isConverting() &&
      count > 0 &&
      count <= MAX_BATCH_SIZE &&
      parsed.invalidLineNumbers.length === 0
    );
  });

  protected onPickChange(pick: KeyMappingPick | null): void {
    const previousKeyFields = this.pick()?.keyFields.join();

    this.pick.set(pick);

    // A result for other key fields no longer matches its columns.
    if (pick?.keyFields.join() !== previousKeyFields) {
      this.result.set(null);
    }
  }

  protected onInputChanged(text: string): void {
    const textSignal =
      this.direction() === 'toNumoKeys' ? this.connectorKeysText : this.numoKeysText;

    textSignal.set(text);
  }

  protected convert(): void {
    const selection = this.selection();

    if (!selection || !this.canConvert()) {
      return;
    }

    const direction = this.direction();

    this.isConverting.set(true);
    this.errorMessage.set(null);

    this.request(selection, direction).subscribe({
      next: (result) => {
        this.result.set(result);
        this.resultDirection.set(direction);
        this.isConverting.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.result.set(null);
        this.isConverting.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }

  private request(
    selection: KeyMappingSelection,
    direction: KeyMappingDirection,
  ): Observable<KeyMappingResult> {
    return direction === 'toNumoKeys'
      ? this.keyMappingApi.convertToNumoKeys(selection.target, this.parsedConnectorKeys().keys)
      : this.keyMappingApi.convertToConnectorKeys(selection.target, this.parsedNumoKeys().keys);
  }
}
