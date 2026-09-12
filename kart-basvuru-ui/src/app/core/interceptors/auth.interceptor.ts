import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).getAccessToken();
  const authService = inject(AuthService);
  const router = inject(Router);
  const isInternalApiRequest = isSameOriginApiRequest(request.url);
  const requestPath = getRequestPath(request.url);
  const isCustomerPortalRequest = isInternalApiRequest && requestPath.startsWith('/api/customer-portal/');
  const isLoginRequest = isInternalApiRequest && requestPath === '/api/auth/login';
  const hasExplicitAuthorization = request.headers.has('Authorization');
  const authorizedRequest = token && isInternalApiRequest && !isCustomerPortalRequest && !hasExplicitAuthorization
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorizedRequest).pipe(catchError((error) => {
    // Harici servislerdeki 401 yanıtı personel oturumunu sonlandırmamalıdır.
    // Bu kontrol aynı zamanda kurum JWT'sinin üçüncü taraf adres servislerine
    // gönderilmesini engeller.
    if (error.status === 401 && isInternalApiRequest) {
      if (isCustomerPortalRequest) {
        sessionStorage.removeItem('customer-portal-auth');
        void router.navigate(['/customer/login'], { queryParams: { sessionExpired: true }, replaceUrl: true });
      } else if (!isLoginRequest) {
        authService.logout();
        void router.navigate(['/login'], { queryParams: { sessionExpired: true }, replaceUrl: true });
      }
    }
    return throwError(() => error);
  }));
};

function isSameOriginApiRequest(url: string): boolean {
  if (/^\/api(?:\/|$)/.test(url)) return true;
  if (typeof window === 'undefined') return false;
  try {
    const parsed = new URL(url, window.location.origin);
    return parsed.origin === window.location.origin && /^\/api(?:\/|$)/.test(parsed.pathname);
  } catch {
    return false;
  }
}

function getRequestPath(url: string): string {
  if (typeof window === 'undefined') return url.split('?')[0];
  try {
    return new URL(url, window.location.origin).pathname;
  } catch {
    return url.split('?')[0];
  }
}
