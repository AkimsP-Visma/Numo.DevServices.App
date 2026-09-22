import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavigationItem {
  readonly label: string;
  readonly route: string;
  /** Both browser sections share one route and one picker; this is what preselects which resources
   * the picker offers. See docs/superpowers/specs/2026-09-21-service-data-browsing-design.md 3.5. */
  readonly queryParams?: Record<string, string>;
}

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppLayout {
  protected readonly navigationItems: readonly NavigationItem[] = [
    { label: 'Swagger', route: '/swagger' },
    { label: 'Service status', route: '/service-health' },
    { label: 'Personnel Browser', route: '/service-data', queryParams: { section: 'Personnel' } },
    {
      label: 'DataIntegration Browser',
      route: '/service-data',
      queryParams: { section: 'DataIntegration' },
    },
    { label: 'Feature toggles', route: '/feature-flags' },
    { label: 'Sample items', route: '/sample-items' },
  ];
}
