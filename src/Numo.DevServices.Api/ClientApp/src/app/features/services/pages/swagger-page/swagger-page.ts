import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProgressBarModule } from 'primeng/progressbar';
import { SelectModule } from 'primeng/select';
import type SwaggerUI from 'swagger-ui';
import { ScriptLoader } from '../../../../shared/helpers/script-loader';
import { StyleLoader } from '../../../../shared/helpers/style-loader';
import { NumoService } from '../../api/numo-service.model';
import { ServicesApiService } from '../../api/services-api.service';

declare global {
  // eslint-disable-next-line no-var
  var SwaggerUIBundle: ((config: Record<string, unknown>) => SwaggerUI) | undefined;
}

const SWAGGER_STYLE_URL = 'swagger-ui.css';
const SWAGGER_SCRIPT_URL = 'swagger-ui-bundle.js';

@Component({
  selector: 'app-swagger-page',
  imports: [FormsModule, SelectModule, ProgressBarModule],
  templateUrl: './swagger-page.html',
  styleUrl: './swagger-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SwaggerPage implements OnInit {
  private readonly servicesApi = inject(ServicesApiService);
  private readonly swaggerContainer = viewChild<ElementRef<HTMLDivElement>>('swaggerContainer');

  // Not readonly: PrimeNG's [options] input takes a mutable array.
  protected readonly services = signal<NumoService[]>([]);
  protected readonly selectedServiceName = signal<string | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.servicesApi.getAll().subscribe({
      next: (services) => this.services.set(services),
      error: () => this.errorMessage.set('Could not load the list of services.'),
    });
  }

  protected onServiceSelected(serviceName: string): void {
    this.selectedServiceName.set(serviceName);
    this.renderSpecification(serviceName).catch((error: unknown) => {
      this.isLoading.set(false);
      this.errorMessage.set('Could not load Swagger UI.');
      console.error(error);
    });
  }

  private async renderSpecification(serviceName: string): Promise<void> {
    this.errorMessage.set(null);
    this.isLoading.set(true);

    await Promise.all([
      StyleLoader.addExternalStyles(SWAGGER_STYLE_URL),
      ScriptLoader.addExternalScript(SWAGGER_SCRIPT_URL),
    ]);

    const container = this.swaggerContainer()?.nativeElement;
    const swaggerUiBundle = globalThis.SwaggerUIBundle;

    if (!container || !swaggerUiBundle) {
      this.isLoading.set(false);
      this.errorMessage.set('Swagger UI is not available.');
      return;
    }

    // The bundle appends to the node it is given, so the previous service's UI has to go first.
    container.replaceChildren();

    swaggerUiBundle({
      domNode: container,
      url: this.servicesApi.getOpenApiUrl(serviceName),
      onComplete: () => this.isLoading.set(false),
      onFailure: () => {
        this.isLoading.set(false);
        this.errorMessage.set(`Could not read the OpenAPI specification of ${serviceName}.`);
      },
    });
  }
}
