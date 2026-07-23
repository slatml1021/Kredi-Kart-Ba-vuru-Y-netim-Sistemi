import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateCustomerRequest, Customer, UpdateCustomerRequest } from './customer.models';

@Injectable({ providedIn: 'root' })
export class CustomerApiService {
  private readonly apiUrl = '/api/customers';

  constructor(private readonly httpClient: HttpClient) {}

  search(query: string): Observable<Customer[]> {
    const params = new HttpParams().set('query', query);
    return this.httpClient.get<Customer[]>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Customer> {
    return this.httpClient.get<Customer>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<Customer> {
    return this.httpClient.post<Customer>(this.apiUrl, request);
  }

  update(id: number, request: UpdateCustomerRequest): Observable<Customer> {
    return this.httpClient.put<Customer>(`${this.apiUrl}/${id}`, request);
  }
}
