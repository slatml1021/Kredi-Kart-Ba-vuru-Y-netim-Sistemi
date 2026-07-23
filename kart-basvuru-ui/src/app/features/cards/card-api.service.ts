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
  customerFullName: string;
  cardTypeName: string;
}

@Injectable({ providedIn: 'root' })
export class CardApiService {
  constructor(private readonly http: HttpClient) {}
  getById(id: number): Observable<CreditCard> { return this.http.get<CreditCard>(`/api/cards/${id}`); }
}
