import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface AddressOption {
  id: number;
  name: string;
  postalCode?: string;
  postalCodeStatus?: 'official' | 'derived' | 'estimated';
}

export interface StreetOption { id: number; name: string; }

interface CatalogResponse {
  data: AddressOption[];
}

@Injectable({ providedIn: 'root' })
export class AddressCatalogService {
  private readonly baseUrl = 'https://api.turkiyeapi.dev/v2';

  constructor(private readonly httpClient: HttpClient) {}

  getProvinces(): Observable<AddressOption[]> {
    return this.get(`${this.baseUrl}/provinces`, 100);
  }

  getDistricts(provinceId: number): Observable<AddressOption[]> {
    return this.get(`${this.baseUrl}/provinces/${provinceId}/districts`, 1000);
  }

  getNeighborhoods(districtId: number): Observable<AddressOption[]> {
    const params = new HttpParams()
      .set('fields', 'id,name,postalCode,postalCodeStatus')
      .set('limit', 1000);
    return this.httpClient.get<CatalogResponse>(
      `${this.baseUrl}/districts/${districtId}/neighborhoods`, { params },
    ).pipe(map((response) => [...response.data].sort((left, right) =>
      left.name.localeCompare(right.name, 'tr'))));
  }

  getStreets(neighborhoodId: number, search = ''): Observable<StreetOption[]> {
    let params = new HttpParams()
      .set('neighborhoodId', neighborhoodId)
      .set('limit', 1000);
    if (search.trim()) params = params.set('search', search.trim());
    return this.httpClient.get<StreetOption[]>('/api/address-catalog/streets', { params });
  }

  private get(url: string, limit: number): Observable<AddressOption[]> {
    const params = new HttpParams()
      .set('fields', 'id,name')
      .set('limit', limit);
    return this.httpClient.get<CatalogResponse>(url, { params }).pipe(
      map((response) => [...response.data].sort((left, right) =>
        left.name.localeCompare(right.name, 'tr'))),
    );
  }
}
