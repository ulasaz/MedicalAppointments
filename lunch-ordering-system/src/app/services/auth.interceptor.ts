import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ToastService } from './toast/toast';
import { TranslateService } from '@ngx-translate/core';
import { TenantService } from './tenant.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const toast = inject(ToastService);
  const translate = inject(TranslateService);
  const token = localStorage.getItem('jwt_token');
  const tenantService = inject(TenantService);

  if(req.url.includes('/i18n')){
    return next(req);
    }

    const tenantId = tenantService.getTenantId();
  const lang = translate.getCurrentLang() || localStorage.getItem('language') || 'en';

  req = req.clone({
    setHeaders: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      'X-Tenant-Id': tenantId,
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
