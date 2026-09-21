import { Routes } from '@angular/router';

export const featureFlagsRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/feature-flags-page/feature-flags-page').then((m) => m.FeatureFlagsPage),
  },
];
