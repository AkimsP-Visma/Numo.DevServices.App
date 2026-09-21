import { Routes } from '@angular/router';

export const serviceHealthRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/service-health-page/service-health-page').then((m) => m.ServiceHealthPage),
  },
];
