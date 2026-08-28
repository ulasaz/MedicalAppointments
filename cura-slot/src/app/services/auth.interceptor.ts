import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ToastService } from './toast/toast';
import { TranslateService } from '@ngx-translate/core';
import { TenantService } from './tenant/tenant';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const toast = inject(ToastService);
  const translate = inject(TranslateService);
  const tenantService = inject(TenantService);
  const token = localStorage.getItem('jwt_token');

  if(req.url.includes('/i18n')){
    return next(req);
    }

  const lang = translate.getCurrentLang() || localStorage.getItem('language') || 'en';
  // Only matters when there's no token (Doctors.API/Appointments.API prefer the JWT's own
  // tenant_id claim over this header) — but it's what lets anonymous doctor search and
  // registration resolve a tenant at all.
  const tenantId = tenantService.selectedTenantId();

  req = req.clone({
    setHeaders: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
      'Accept-Language': lang,
    }
  });

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        localStorage.removeItem('jwt_token');
        toast.error(translate.instant('AUTH.SESSION_EXPIRED'));
        router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
