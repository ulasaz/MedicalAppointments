import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { provideTranslateService, TranslateService, TranslationObject } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './services/auth.interceptor';
import { TenantService } from './services/tenant.service';

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
      const lang = localStorage.getItem('language') ?? 'pl';
      try {
        const translations = await firstValueFrom(http.get<TranslationObject>(`/i18n/${lang}.json`));
        translate.setTranslation(lang, translations);
        translate.use(lang);
      } catch {}
    }),
    provideAppInitializer(() => inject(TenantService).loadConfig()),
  ]
};
