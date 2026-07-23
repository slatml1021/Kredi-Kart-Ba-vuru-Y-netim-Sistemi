import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CardApplication, CardApplicationDetail, CardType, CreateCardApplicationRequest, EvaluateCardApplicationRequest, ResubmitCardApplicationRequest } from './card-application.models';

@Injectable({ providedIn: 'root' })
export class CardApplicationApiService {
  private readonly apiUrl = '/api/card-applications';

  constructor(private readonly httpClient: HttpClient) {}

  getCardTypes(): Observable<CardType[]> {
    return this.httpClient.get<CardType[]>(`${this.apiUrl}/card-types`);
  }

  create(request: CreateCardApplicationRequest): Observable<CardApplication> {
    return this.httpClient.post<CardApplication>(this.apiUrl, request);
  }

  getById(id: number): Observable<CardApplication> {
    return this.httpClient.get<CardApplication>(`${this.apiUrl}/${id}`);
  }

  getDetail(id: number): Observable<CardApplicationDetail> {
    return this.httpClient.get<CardApplicationDetail>(`${this.apiUrl}/${id}/detail`);
  }

  getMine(): Observable<CardApplication[]> {
    return this.httpClient.get<CardApplication[]>(`${this.apiUrl}/mine`);
  }

  getPending(): Observable<CardApplication[]> {
    return this.httpClient.get<CardApplication[]>(`${this.apiUrl}/pending`);
  }

  evaluate(id: number, request: EvaluateCardApplicationRequest): Observable<CardApplication> {
    return this.httpClient.post<CardApplication>(`${this.apiUrl}/${id}/evaluation`, request);
  }

  resubmit(id: number, request: ResubmitCardApplicationRequest): Observable<CardApplication> {
    return this.httpClient.post<CardApplication>(`${this.apiUrl}/${id}/resubmit`, request);
  }
}
