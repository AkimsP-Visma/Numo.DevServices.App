import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'sample-items' },
  {
    path: 'sample-items',
    loadChildren: () => import('./features/sample-items/sample-items.routes').then((m) => m.sampleItemsRoutes),
  },
];
