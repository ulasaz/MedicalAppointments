import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface MedicalCenter {
  id: string;
  name: string;
  slug: string;
  primaryColorHex: string;
  isActive: boolean;
  createdAt: string;
  fontFamily: string;
  buttonRadius: 'pill' | 'rounded' | 'sharp';
  bannerVideoUrl?: string | null;
  hasBannerImage: boolean;
}

export interface CreateMedicalCenterRequest {
  name: string;
  slug: string;
  primaryColorHex: string;
  adminEmail: string;
  adminPassword: string;
  adminDisplayName: string;
}

export interface UpdateMedicalCenterRequest {
  name: string;
  primaryColorHex: string;
  fontFamily: string;
  buttonRadius: string;
  bannerVideoUrl?: string | null;
}

const STORAGE_KEY = 'tenant_id';
const DEFAULT_COLOR = '#f43f5e';

/** Curated so the frontend can preload every option once (see index.html) instead of
 * fetching an arbitrary font stylesheet at runtime — matches the server-side allow-list. */
export const AVAILABLE_FONTS: { name: string; stack: string }[] = [
  { name: 'Inter', stack: "'Inter', system-ui, sans-serif" },
  { name: 'Poppins', stack: "'Poppins', system-ui, sans-serif" },
  { name: 'Montserrat', stack: "'Montserrat', system-ui, sans-serif" },
  { name: 'Merriweather', stack: "'Merriweather', Georgia, serif" },
  { name: 'Roboto Slab', stack: "'Roboto Slab', Georgia, serif" },
  { name: 'Playfair Display', stack: "'Playfair Display', Georgia, serif" },
];

export const AVAILABLE_RADII: { value: 'pill' | 'rounded' | 'sharp'; px: string }[] = [
  { value: 'pill', px: '9999px' },
  { value: 'rounded', px: '0.75rem' },
  { value: 'sharp', px: '0.25rem' },
];

/**
 * Tracks which medical center the app is currently browsing as. A logged-in user's own
 * center (from their JWT's tenant_id claim) always wins over whatever was locally picked
 * pre-login — otherwise this drives anonymous browsing/registration and is what the
 * X-Tenant-Id header (see auth.interceptor.ts) falls back to when there's no token yet.
 */
@Injectable({
  providedIn: 'root',
})
export class TenantService {
  private httpClient = inject(HttpClient);

  readonly centers = signal<MedicalCenter[]>([]);
  readonly selectedTenantId = signal<string | null>(localStorage.getItem(STORAGE_KEY));
  readonly selectedTenant = signal<MedicalCenter | null>(null);

  loadAll() {
    return this.httpClient.get<MedicalCenter[]>(`${environment.gatewayApiUrl}/tenants`).pipe(
      tap((centers) => {
        this.centers.set(centers);
        this.reconcileSelection(centers);
      })
    );
  }

  /** Called once after loadAll(), and again whenever a fresh JWT is set (login/register). */
  syncFromTenantId(tenantIdFromToken: string | null) {
    if (tenantIdFromToken) {
      this.selectTenant(tenantIdFromToken);
      return;
    }
    // No claim (anonymous, or the platform super-admin) — keep whatever's already selected.
    this.reconcileSelection(this.centers());
  }

  private reconcileSelection(centers: MedicalCenter[]) {
    if (centers.length === 0) return;

    const current = this.selectedTenantId();
    const stillExists = current && centers.some((c) => c.id === current);
    const tenantId = stillExists ? current! : centers[0].id;

    this.selectTenant(tenantId, centers);
  }

  selectTenant(tenantId: string, knownCenters?: MedicalCenter[]) {
    localStorage.setItem(STORAGE_KEY, tenantId);
    this.selectedTenantId.set(tenantId);

    const center = (knownCenters ?? this.centers()).find((c) => c.id === tenantId) ?? null;
    this.selectedTenant.set(center);
    this.applyBranding(center);
  }

  create(request: CreateMedicalCenterRequest) {
    return this.httpClient.post<MedicalCenter>(`${environment.gatewayApiUrl}/tenants`, request);
  }

  update(id: string, request: UpdateMedicalCenterRequest) {
    return this.httpClient.put<MedicalCenter>(`${environment.gatewayApiUrl}/tenants/${id}`, request).pipe(
      tap((updated) => this.applyUpdatedCenter(id, updated))
    );
  }

  bannerImageUrl(tenantId: string): string {
    return `${environment.gatewayApiUrl}/tenants/${tenantId}/banner`;
  }

  uploadBanner(tenantId: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.httpClient.put(`${environment.gatewayApiUrl}/tenants/${tenantId}/banner`, formData).pipe(
      tap(() => {
        this.centers.update((all) => all.map((c) => (c.id === tenantId ? { ...c, hasBannerImage: true } : c)));
        if (this.selectedTenantId() === tenantId) {
          this.selectedTenant.update((c) => (c ? { ...c, hasBannerImage: true } : c));
        }
      })
    );
  }

  deleteBanner(tenantId: string) {
    return this.httpClient.delete(`${environment.gatewayApiUrl}/tenants/${tenantId}/banner`).pipe(
      tap(() => {
        this.centers.update((all) => all.map((c) => (c.id === tenantId ? { ...c, hasBannerImage: false } : c)));
        if (this.selectedTenantId() === tenantId) {
          this.selectedTenant.update((c) => (c ? { ...c, hasBannerImage: false } : c));
        }
      })
    );
  }

  private applyUpdatedCenter(id: string, updated: MedicalCenter) {
    this.centers.update((all) => all.map((c) => (c.id === id ? updated : c)));
    if (this.selectedTenantId() === id) {
      this.selectedTenant.set(updated);
      this.applyBranding(updated);
    }
  }

  /** Derives hover/light/ring shades from the one admin-picked hex via color-mix(), rather
   * than requiring a full Tailwind-style 50-900 scale per center. Font/radius map straight
   * from the curated lists above onto their own CSS custom properties. */
  private applyBranding(center: MedicalCenter | null) {
    const hex = center?.primaryColorHex ?? DEFAULT_COLOR;
    const fontStack = AVAILABLE_FONTS.find((f) => f.name === center?.fontFamily)?.stack ?? AVAILABLE_FONTS[0].stack;
    const radiusPx = AVAILABLE_RADII.find((r) => r.value === center?.buttonRadius)?.px ?? AVAILABLE_RADII[0].px;

    const root = document.documentElement.style;
    root.setProperty('--brand-primary', hex);
    root.setProperty('--brand-primary-hover', `color-mix(in srgb, ${hex} 85%, black)`);
    root.setProperty('--brand-primary-light', `color-mix(in srgb, ${hex} 12%, white)`);
    root.setProperty('--brand-ring', `color-mix(in srgb, ${hex} 40%, transparent)`);
    root.setProperty('--brand-font', fontStack);
    root.setProperty('--brand-radius', radiusPx);
  }
}
