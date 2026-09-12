import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface OfficerDashboard {
  totalApplications: number;
  pendingApplications: number;
  revisionApplications: number;
  approvedApplications: number;
  rejectedApplications: number;
  recentRequests: RecentOfficerRequest[];
}

export interface RecentOfficerRequest {
  requestType: 'CardApplication' | 'SupplementaryCard' | 'LimitIncrease' | 'LimitDecrease';
  entityId: number;
  referenceNumber: string;
  customerId: number;
  customerName: string;
  description: string;
  status: string;
  createdAtUtc: string;
}

export interface ManagerDashboard {
  pendingApplications: number;
  approvedToday: number;
  rejectedToday: number;
  revisionApplications: number;
  processedApplications: number;
  approvedApplications: number;
  rejectedApplications: number;
  approvalRate: number;
  rejectionRate: number;
  totalApplications: number;
  cardTypeDistribution: { cardType: string; count: number; percentage: number }[];
  applicationTrend: { label: string; count: number }[];
  officerPerformance: { officerName: string; createdApplications: number; approvalRate: number; averageProcessingMinutes: number }[];
}

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  constructor(private readonly http: HttpClient) {}

  officer(): Observable<OfficerDashboard> {
    return this.http.get<OfficerDashboard>('/api/dashboard/officer');
  }

  manager(period = 'Week', month?: number, year?: number): Observable<ManagerDashboard> {
    const params: Record<string, string | number> = { period };
    if (month) params['month'] = month;
    if (year) params['year'] = year;
    return this.http.get<ManagerDashboard>('/api/dashboard/manager', { params });
  }
}
