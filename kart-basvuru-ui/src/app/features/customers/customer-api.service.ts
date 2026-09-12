import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ContactVerification, CreateCustomerRequest, Customer, CustomerAddress, CustomerAddressRequest, CustomerDetailData, ExternalRiskProfile, UpdateCustomerRequest } from './customer.models';

@Injectable({ providedIn: 'root' })
export class CustomerApiService {
  private readonly apiUrl = '/api/customers';

  constructor(private readonly httpClient: HttpClient) {}

  getAll(): Observable<Customer[]> {
    return this.httpClient.get<Customer[]>(`${this.apiUrl}/list`);
  }

  getExternalRiskProfile(nationalIdentityNumber: string): Observable<ExternalRiskProfile> {
    const params = new HttpParams().set('nationalIdentityNumber', nationalIdentityNumber);
    return this.httpClient.get<ExternalRiskProfile>(`${this.apiUrl}/external-risk-profile`, { params });
  }

  search(query: string, criterion?: string): Observable<Customer[]> {
    let params = new HttpParams().set('query', query);
    if (criterion) params = params.set('criterion', criterion);
    return this.httpClient.get<Customer[]>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Customer> {
    return this.httpClient.get<Customer>(`${this.apiUrl}/${id}`);
  }

  getDetailById(id: number): Observable<CustomerDetailData> {
    return this.httpClient.get<CustomerDetailData>(`${this.apiUrl}/${id}/detail`);
  }

  create(request: CreateCustomerRequest): Observable<Customer> {
    return this.httpClient.post<Customer>(this.apiUrl, request);
  }

  update(id: number, request: UpdateCustomerRequest): Observable<Customer> {
    return this.httpClient.put<Customer>(`${this.apiUrl}/${id}`, request);
  }

  verifyContact(id: number, channel: 'Phone' | 'Email', code: string): Observable<Customer> {
    return this.httpClient.post<Customer>(`${this.apiUrl}/${id}/verify-contact`, { channel, code });
  }

  requestContactVerification(id: number, channel: 'Phone' | 'Email'): Observable<ContactVerification> {
    return this.httpClient.post<ContactVerification>(
      `${this.apiUrl}/${id}/request-contact-verification`,
      { channel },
    );
  }

  setStatus(id: number, isActive: boolean): Observable<Customer> {
    return this.httpClient.patch<Customer>(`${this.apiUrl}/${id}/status`, { isActive });
  }

  getAddresses(customerId: number): Observable<CustomerAddress[]> {
    return this.httpClient.get<CustomerAddress[]>(`${this.apiUrl}/${customerId}/addresses`);
  }

  addAddress(customerId: number, request: CustomerAddressRequest): Observable<CustomerAddress> {
    return this.httpClient.post<CustomerAddress>(`${this.apiUrl}/${customerId}/addresses`, request);
  }

  updateAddress(customerId: number, addressId: number, request: CustomerAddressRequest): Observable<CustomerAddress> {
    return this.httpClient.put<CustomerAddress>(`${this.apiUrl}/${customerId}/addresses/${addressId}`, request);
  }

  setDefaultAddress(customerId: number, addressId: number): Observable<CustomerAddress> {
    return this.httpClient.patch<CustomerAddress>(`${this.apiUrl}/${customerId}/addresses/${addressId}/default`, {});
  }

  deleteAddress(customerId: number, addressId: number): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiUrl}/${customerId}/addresses/${addressId}`);
  }
}
