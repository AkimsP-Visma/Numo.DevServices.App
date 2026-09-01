import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { SampleItemsApiService } from '../../api/sample-items-api.service';
import { SampleItem } from '../../api/sample-item.model';
import { PagedRequest } from '../../../../shared/api/paginated-list.model';

const NAME_MAX_LENGTH = 56;
const DESCRIPTION_MAX_LENGTH = 2000;
const FIRST_PAGE: PagedRequest = { page: 1, limit: 20, direction: 'Asc' };

@Component({
  selector: 'app-sample-items-page',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    TableModule,
    TextareaModule,
  ],
  templateUrl: './sample-items-page.html',
  styleUrl: './sample-items-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SampleItemsPage {
  private readonly sampleItemsApi = inject(SampleItemsApiService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly items = signal<SampleItem[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly isDialogOpen = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  protected readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;

  protected readonly createForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)]],
    description: ['', [Validators.maxLength(DESCRIPTION_MAX_LENGTH)]],
  });

  constructor() {
    this.load();
  }

  protected openCreateDialog(): void {
    this.createForm.reset();
    this.errorMessage.set(null);
    this.isDialogOpen.set(true);
  }

  protected create(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const { name, description } = this.createForm.getRawValue();

    this.isSaving.set(true);
    this.errorMessage.set(null);

    this.sampleItemsApi.create({ name, description: description || null }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.isDialogOpen.set(false);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.isSaving.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }

  protected load(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.sampleItemsApi.getAll(FIRST_PAGE).subscribe({
      next: (page) => {
        this.items.set([...page.items]);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }
}

// The API reports failures as RFC 7807 problem details produced by the NumoResult filter.
function readProblemDetail(error: HttpErrorResponse): string {
  return error.error?.detail ?? error.error?.title ?? error.message;
}
