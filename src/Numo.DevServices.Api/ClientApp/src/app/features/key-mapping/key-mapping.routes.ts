import { Routes } from '@angular/router';

export const keyMappingRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/key-mapping-page/key-mapping-page').then((m) => m.KeyMappingPage),
  },
];
