import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).getAccessToken();
  const authService = inject(AuthService);
  const router = inject(Router);
  const authorizedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorizedRequest).pipe(catchError((error) => {
    if (error.status === 401 && !request.url.endsWith('/api/auth/login')) {
      authService.logout();
      void router.navigate(['/login'], { queryParams: { sessionExpired: true } });
    }
    return throwError(() => error);
  }));
};
