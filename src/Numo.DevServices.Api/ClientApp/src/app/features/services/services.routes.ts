import { Routes } from '@angular/router';

export const servicesRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/swagger-page/swagger-page').then((m) => m.SwaggerPage),
  },
];
