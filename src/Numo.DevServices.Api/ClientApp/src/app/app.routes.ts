import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'swagger' },
  {
    path: 'swagger',
    loadChildren: () => import('./features/services/services.routes').then((m) => m.servicesRoutes),
  },
  {
    path: 'sample-items',
    loadChildren: () => import('./features/sample-items/sample-items.routes').then((m) => m.sampleItemsRoutes),
  },
];
