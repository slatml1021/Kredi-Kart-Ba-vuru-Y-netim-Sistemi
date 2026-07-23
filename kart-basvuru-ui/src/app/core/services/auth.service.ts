import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { map, Observable } from 'rxjs';
import { CurrentUser, LoginRequest, LoginResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly storageKey = 'credit-card-auth';
  private readonly currentUserState = signal<CurrentUser | null>(this.restoreUser());

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserState() !== null);

  constructor(private readonly httpClient: HttpClient) {}

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.httpClient.post<LoginResponse>('/api/auth/login', request).pipe(
      map((response) => {
        const role = response.user.roles[0];
        if (!role) throw new Error('Kullanıcıya atanmış bir rol bulunamadı.');
        const user: CurrentUser = {
          id: response.user.id,
          registrationNumber: response.user.registrationNumber,
          fullName: response.user.fullName,
          role,
        };
        sessionStorage.setItem(AuthService.storageKey, JSON.stringify({ user, accessToken: response.accessToken, expiresAtUtc: response.expiresAtUtc }));
        this.currentUserState.set(user);
        return user;
      })
    );
  }

  logout(): void {
    sessionStorage.removeItem(AuthService.storageKey);
    this.currentUserState.set(null);
  }

  getAccessToken(): string | null {
    const stored = sessionStorage.getItem(AuthService.storageKey);
    if (!stored) return null;
    const session = JSON.parse(stored) as { accessToken: string; expiresAtUtc: string };
    return new Date(session.expiresAtUtc) > new Date() ? session.accessToken : null;
  }

  private restoreUser(): CurrentUser | null {
    const stored = sessionStorage.getItem(AuthService.storageKey);
    if (!stored) return null;
    const session = JSON.parse(stored) as { user: CurrentUser; expiresAtUtc: string };
    return new Date(session.expiresAtUtc) > new Date() ? session.user : null;
  }
}
