import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'swagger' },
  {
    path: 'swagger',
    loadChildren: () => import('./features/services/services.routes').then((m) => m.servicesRoutes),
  },
  {
    path: 'feature-flags',
    loadChildren: () =>
      import('./features/feature-flags/feature-flags.routes').then((m) => m.featureFlagsRoutes),
  },
  {
    path: 'sample-items',
    loadChildren: () => import('./features/sample-items/sample-items.routes').then((m) => m.sampleItemsRoutes),
  },
];
