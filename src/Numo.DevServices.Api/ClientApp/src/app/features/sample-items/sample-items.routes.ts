import { Routes } from '@angular/router';

export const sampleItemsRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/sample-items-page/sample-items-page').then((m) => m.SampleItemsPage),
  },
];
