import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface CreditCard {
  id: number;
  maskedCardNumber: string;
  cardLimit: number;
  status: string;
  issueDate: string;
  expiryDate: string;
  applicationId: number;
  applicationNumber: string;
  customerNumber: string;
  customerFullName: string;
  cardTypeName: string;
  deliveryMethod: string;
  deliveryAddress: string;
  statementPreference: string;
  contactlessEnabled: boolean;
  internetShoppingEnabled: boolean;
  cardTypeMaximumLimit: number;
  customerMaximumLimit: number;
  requestedNewLimit: number | null;
  limitIncreaseStatus: string | null;
  limitChangeType: 'Increase' | 'Decrease' | null;
  limitIncreaseRequestedAtUtc: string | null;
  limitIncreaseEvaluationNote: string | null;
  limitIncreaseEvaluatedAtUtc: string | null;
}

export interface LimitIncreaseRequest {
  cardId: number;
  maskedCardNumber: string;
  customerNumber: string;
  customerFullName: string;
  cardTypeName: string;
  currentLimit: number;
  requestedNewLimit: number;
  changeType: 'Increase' | 'Decrease';
  status: string;
  requestedAtUtc: string;
  evaluationNote: string | null;
  evaluatedAtUtc: string | null;
}

@Injectable({ providedIn: 'root' })
export class CardApiService {
  constructor(private readonly http: HttpClient) {}
  getById(id: number): Observable<CreditCard> { return this.http.get<CreditCard>(`/api/cards/${id}`); }
  requestLimitIncrease(id: number, requestedNewLimit: number): Observable<CreditCard> {
    return this.http.post<CreditCard>(`/api/cards/${id}/limit-increase`, { requestedNewLimit });
  }
  requestLimitChange(id: number, changeType: 'Increase' | 'Decrease', requestedNewLimit: number): Observable<CreditCard> {
    return this.http.post<CreditCard>(`/api/cards/${id}/limit-change`, { changeType, requestedNewLimit });
  }
  getLimitIncreases(): Observable<LimitIncreaseRequest[]> {
    return this.http.get<LimitIncreaseRequest[]>('/api/cards/limit-increases');
  }
  evaluateLimitIncrease(id: number, decision: 'Approved' | 'Rejected', note: string): Observable<LimitIncreaseRequest> {
    return this.http.post<LimitIncreaseRequest>(`/api/cards/${id}/limit-increase/evaluate`, { decision, note });
  }
}
