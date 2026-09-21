import { Routes } from '@angular/router';

export const serviceDataRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/service-data-page/service-data-page').then((m) => m.ServiceDataPage),
  },
  {
    path: ':resource',
    loadComponent: () =>
      import('./pages/service-data-page/service-data-page').then((m) => m.ServiceDataPage),
  },
  {
    path: ':resource/:id',
    loadComponent: () =>
      import('./pages/service-data-record-page/service-data-record-page').then(
        (m) => m.ServiceDataRecordPage,
      ),
  },
];
