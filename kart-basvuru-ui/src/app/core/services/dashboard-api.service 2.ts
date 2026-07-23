import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface OfficerDashboard { totalApplications: number; pendingApplications: number; revisionApplications: number; approvedApplications: number; }
export interface ManagerDashboard { pendingApplications: number; approvedToday: number; rejectedToday: number; revisionApplications: number; }

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  constructor(private readonly http: HttpClient) {}
  officer(): Observable<OfficerDashboard> { return this.http.get<OfficerDashboard>('/api/dashboard/officer'); }
  manager(): Observable<ManagerDashboard> { return this.http.get<ManagerDashboard>('/api/dashboard/manager'); }
}
