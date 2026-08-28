import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { provideTranslateService, TranslateService, TranslationObject } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './services/auth.interceptor';
import { TenantService } from './services/tenant/tenant';
import { AuthService } from './services/auth/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideTranslateService({ fallbackLang: 'pl' }),
    ...provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }),
    provideAppInitializer(async () => {
      const http = inject(HttpClient);
      const translate = inject(TranslateService);
      // Cache-bust so a browser/CDN never serves a stale copy of the translation files
      // after new keys are added — each app load fetches them fresh.
      const cacheBust = Date.now();
      await Promise.all(['en', 'pl'].map(async (lang) => {
        try {
          const translations = await firstValueFrom(http.get<TranslationObject>(`/i18n/${lang}.json?v=${cacheBust}`));
          translate.setTranslation(lang, translations);
        } catch {}
      }));
      const lang = localStorage.getItem('language') ?? 'pl';
      translate.use(lang);
    }),
    provideAppInitializer(async () => {
      const tenantService = inject(TenantService);
      const authService = inject(AuthService);
      try {
        await firstValueFrom(tenantService.loadAll());
      } catch {}
      // A page refresh while logged in still has the JWT but the fresh loadAll() call above
      // only reconciles against localStorage — make sure the account's real center wins.
      tenantService.syncFromTenantId(authService.getTenantId());
    }),
  ]
};
