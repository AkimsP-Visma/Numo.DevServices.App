import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavigationItem {
  readonly label: string;
  readonly route: string;
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
    { label: 'Sample items', route: '/sample-items' },
  ];
}
