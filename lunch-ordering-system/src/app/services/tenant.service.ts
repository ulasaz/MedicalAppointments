import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

export interface TenantConfig {
  name: string;
  primaryColor: string;
  secondaryColor: string;
  logoUrl: string;
  backgroundColor: string;
  fontFamily: string;
  borderRadius: string;
  headingColor: string;
  bodyColor: string;
}

@Injectable ({providedIn: 'root'})
export class TenantService {
    private configSubject = new BehaviorSubject<TenantConfig | null>(null);
    config$ = this.configSubject.asObservable();

    constructor(private http: HttpClient){}

    getTenantId(): string {
        const param = new URLSearchParams(window.location.search).get('tenant');
        if (param) return param;
        const host = window.location.hostname;
        const parts = host.split('.');
        return parts.length >= 2 ? parts[0] : 'cafe1';
    }

    loadConfig(): Promise<void> {
        const tenantId = this.getTenantId();

        return new Promise((resolve) =>{
            this.http.get<TenantConfig>(`/tenants/${tenantId}.json`)
            .subscribe({
                next: (config) => {
                    this.configSubject.next(config);
                    this.applyTheme(config)
                    resolve(); 
                }, error: () =>{
                    resolve();
                }
            });    
        });
       
    }

private isDark(hex: string): boolean {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return (0.299 * r + 0.587 * g + 0.114 * b) < 128;
}

private applyTheme(config: TenantConfig): void {
  const root = document.documentElement;
  const dark = this.isDark(config.backgroundColor);

  root.style.setProperty('--color-brand',        config.primaryColor);
  root.style.setProperty('--color-brand-hover',  config.secondaryColor);
  root.style.setProperty('--color-brand-strong',  `color-mix(in srgb, ${config.primaryColor} 80%, black)`);
  root.style.setProperty('--color-brand-medium',  `color-mix(in srgb, ${config.primaryColor} 45%, white)`);
  root.style.setProperty('--color-brand-soft',    `color-mix(in srgb, ${config.primaryColor} 15%, white)`);
  root.style.setProperty('--color-cream',        config.backgroundColor);
  root.style.setProperty('--color-heading',      config.headingColor);
  root.style.setProperty('--color-body',         config.bodyColor);
  root.style.setProperty('--font-sans',          config.fontFamily);
  root.style.setProperty('--font-serif',         config.fontFamily);

  root.style.setProperty('--color-input-bg',          dark ? 'rgba(255,255,255,0.08)' : 'white');
  root.style.setProperty('--color-input-text',        config.headingColor);
  root.style.setProperty('--color-input-border',      dark ? 'rgba(255,255,255,0.15)' : 'rgb(229 231 235)');
  root.style.setProperty('--color-input-placeholder', dark ? 'rgba(255,255,255,0.35)' : 'rgb(156 163 175)');

  // Map tenant borderRadius to Tailwind v4's --radius-* variables so that
  // rounded-sm / rounded-2xl / rounded-3xl etc. all scale together.
  // Scale anchored at "lg" = the tenant value (cafe1: 0.5rem → Tailwind defaults; cafe2: 2rem → very rounded).
  const base = parseFloat(config.borderRadius);
  const unit = config.borderRadius.replace(/[\d.]+/, '') || 'rem';
  const r = (factor: number) => `${+(base * factor).toFixed(3)}${unit}`;
  root.style.setProperty('--radius-sm',   r(0.25));
  root.style.setProperty('--radius',      r(0.5));
  root.style.setProperty('--radius-md',   r(0.75));
  root.style.setProperty('--radius-lg',   r(1));
  root.style.setProperty('--radius-xl',   r(1.5));
  root.style.setProperty('--radius-2xl',  r(2));
  root.style.setProperty('--radius-3xl',  r(3));
  root.style.setProperty('--radius-full', config.borderRadius);
}
}