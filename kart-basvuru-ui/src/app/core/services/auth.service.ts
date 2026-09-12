import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { map, Observable } from 'rxjs';
import { CurrentUser, LoginRequest, LoginResponse } from '../models/auth.models';

interface StoredAuthSession {
  user: CurrentUser;
  accessToken: string;
  expiresAtUtc: string;
}

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
        // Menü tercihi sayfa geçişlerinde korunur; yeni oturum ise tam genişlikte başlar.
        sessionStorage.setItem('sidebar-open', 'false');
        this.currentUserState.set(user);
        return user;
      })
    );
  }

  reportLoginIssue(registrationNumber: string): Observable<void> {
    return this.httpClient.post<void>('/api/auth/report-login-issue', { registrationNumber });
  }

  logout(): void {
    sessionStorage.removeItem(AuthService.storageKey);
    sessionStorage.removeItem('sidebar-open');
    this.currentUserState.set(null);
  }

  endSession(): Observable<void> {
    return this.httpClient.post<void>('/api/auth/logout', {});
  }

  getAccessToken(): string | null {
    return this.readStoredSession()?.accessToken ?? null;
  }

  private restoreUser(): CurrentUser | null {
    return this.readStoredSession()?.user ?? null;
  }

  private readStoredSession(): StoredAuthSession | null {
    if (typeof sessionStorage === 'undefined') return null;
    const stored = sessionStorage.getItem(AuthService.storageKey);
    if (!stored) return null;

    try {
      const session = JSON.parse(stored) as Partial<StoredAuthSession>;
      const user = session.user;
      const expiresAt = typeof session.expiresAtUtc === 'string'
        ? Date.parse(session.expiresAtUtc)
        : Number.NaN;
      const isValidUser = !!user
        && Number.isInteger(user.id)
        && typeof user.registrationNumber === 'string'
        && user.registrationNumber.length > 0
        && typeof user.fullName === 'string'
        && user.fullName.length > 0
        && (user.role === 'Officer' || user.role === 'Manager');

      if (!isValidUser
          || typeof session.accessToken !== 'string'
          || session.accessToken.length === 0
          || !Number.isFinite(expiresAt)
          || expiresAt <= Date.now()) {
        sessionStorage.removeItem(AuthService.storageKey);
        return null;
      }

      return session as StoredAuthSession;
    } catch {
      // Eski ya da bozulmuş tarayıcı kaydı giriş ekranını tamamen kırmamalıdır.
      sessionStorage.removeItem(AuthService.storageKey);
      return null;
    }
  }
}
