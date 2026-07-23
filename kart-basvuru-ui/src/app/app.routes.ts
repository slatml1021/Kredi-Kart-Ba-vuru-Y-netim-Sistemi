import { Routes } from '@angular/router';
import { authGuard, managerGuard, officerGuard } from './core/guards/auth.guard';
import { Login } from './features/auth/login/login';
import { ManagerDashboard } from './features/dashboard/manager-dashboard/manager-dashboard';
import { OfficerDashboard } from './features/dashboard/officer-dashboard/officer-dashboard';
import { CustomerSearch } from './features/customers/customer-search/customer-search';
import { CustomerCreate } from './features/customers/customer-create/customer-create';
import { CustomerDetail } from './features/customers/customer-detail/customer-detail';
import { ApplicationCreate } from './features/applications/application-create/application-create';
import { ApplicationList } from './features/applications/application-list/application-list';
import { ApplicationReview } from './features/applications/application-review/application-review';
import { ApplicationDetail } from './features/applications/application-detail/application-detail';
import { CardDetail } from './features/cards/card-detail/card-detail';
import { AppShell } from './shared/layout/app-shell/app-shell';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: Login },
  {
    path: 'officer', component: AppShell, canActivate: [authGuard, officerGuard], data: { mode: 'officer' },
    children: [
      { path: 'dashboard', component: OfficerDashboard },
      { path: 'customers', component: CustomerSearch },
      { path: 'customers/new', component: CustomerCreate },
      { path: 'customers/:id', component: CustomerDetail },
      { path: 'applications/new', component: ApplicationCreate },
      { path: 'applications', component: ApplicationList },
      { path: 'applications/:id', component: ApplicationDetail },
      { path: 'cards/:id', component: CardDetail },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  {
    path: 'manager', component: AppShell, canActivate: [authGuard, managerGuard], data: { mode: 'manager' },
    children: [
      { path: 'dashboard', component: ManagerDashboard },
      { path: 'applications', component: ApplicationReview },
      { path: 'applications/:id', component: ApplicationDetail },
      { path: 'cards/:id', component: CardDetail },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
