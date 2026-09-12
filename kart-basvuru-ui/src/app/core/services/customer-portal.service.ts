import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';

interface CustomerSession {
  accessToken: string; expiresAtUtc: string; customerId: number; customerNumber: string; fullName: string;
}

export interface CustomerPortalDashboard {
  customer: { customerNumber: string; fullName: string };
  applications: { id: number; applicationNumber: string; cardType: string; requestedLimit: number; status: string; createdAtUtc: string }[];
  cards: { id: number; maskedCardNumber: string; cardType: string; cardLimit: number; status: string; fulfillmentStatus: string | null; estimatedDeliveryAtUtc: string | null }[];
  supplementaryCards: { id: number; applicationNumber: string; maskedCardNumber: string | null; requestedLimit: number; cardStatus: string; fulfillmentStatus: string | null; estimatedDeliveryAtUtc: string | null; primaryCardHolder: string }[];
}

@Injectable({ providedIn: 'root' })
export class CustomerPortalService {
  private readonly key = 'customer-portal-auth';
  readonly session = signal<CustomerSession | null>(this.restore());
  constructor(private readonly http: HttpClient) {}
  login(customerNumber: string, password: string) {
    return this.http.post<CustomerSession>('/api/customer-portal/login', { customerNumber, password }).pipe(tap(session => {
      sessionStorage.setItem(this.key, JSON.stringify(session)); this.session.set(session);
    }));
  }
  dashboard() { return this.http.get<CustomerPortalDashboard>('/api/customer-portal/dashboard', { headers: this.headers() }); }
  requestCardActivationCode(id: number) { return this.http.post<{ channel:string; maskedDestination:string; demoCode:string|null }>(`/api/customer-portal/cards/${id}/activation-code`, {}, { headers: this.headers() }); }
  activateCard(id: number, verificationCode: string) { return this.http.post(`/api/customer-portal/cards/${id}/activate`, { verificationCode }, { headers: this.headers() }); }
  requestSupplementaryActivationCode(id: number) { return this.http.post<{ channel:string; maskedDestination:string; demoCode:string|null }>(`/api/customer-portal/supplementary-cards/${id}/activation-code`, {}, { headers: this.headers() }); }
  activateSupplementaryCard(id: number, verificationCode: string) { return this.http.post(`/api/customer-portal/supplementary-cards/${id}/activate`, { verificationCode }, { headers: this.headers() }); }
  simulate(request: { cardTypeId: number; requestedLimit: number }) {
    return this.http.post<any>('/api/customer-portal/simulation', request, { headers: this.headers() });
  }
  logout() { sessionStorage.removeItem(this.key); this.session.set(null); }
  private headers() { return new HttpHeaders({ Authorization: `Bearer ${this.session()?.accessToken ?? ''}` }); }
  private restore(): CustomerSession | null {
    const raw=sessionStorage.getItem(this.key);if(!raw)return null;const value=JSON.parse(raw) as CustomerSession;
    return new Date(value.expiresAtUtc)>new Date()?value:null;
  }
}
