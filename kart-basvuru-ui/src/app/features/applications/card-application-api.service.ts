import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApplicationDocument, CardApplication, CardApplicationDetail, CardType, CreateCardApplicationRequest, EvaluateCardApplicationRequest, ResubmitCardApplicationRequest } from './card-application.models';

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

  uploadDocument(applicationId: number, documentType: string, file: File): Observable<ApplicationDocument> {
    const formData = new FormData();
    formData.append('documentType', documentType);
    formData.append('file', file, file.name);
    return this.httpClient.post<ApplicationDocument>(`${this.apiUrl}/${applicationId}/documents`, formData);
  }

  getDocuments(applicationId: number): Observable<ApplicationDocument[]> {
    return this.httpClient.get<ApplicationDocument[]>(`${this.apiUrl}/${applicationId}/documents`);
  }

  downloadDocument(applicationId: number, documentId: number): Observable<Blob> {
    return this.httpClient.get(`${this.apiUrl}/${applicationId}/documents/${documentId}`, { responseType: 'blob' });
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

  getAll(): Observable<CardApplication[]> {
    return this.httpClient.get<CardApplication[]>(`${this.apiUrl}/all`);
  }

  evaluate(id: number, request: EvaluateCardApplicationRequest): Observable<CardApplication> {
    return this.httpClient.post<CardApplication>(`${this.apiUrl}/${id}/evaluation`, request);
  }

  resubmit(id: number, request: ResubmitCardApplicationRequest): Observable<CardApplication> {
    return this.httpClient.post<CardApplication>(`${this.apiUrl}/${id}/resubmit`, request);
  }
}
