import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { TenantService } from '../tenant/tenant';

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  role: string;
  createdAt: string;
  isActive: boolean;
  tenantId?: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private httpClient = inject(HttpClient);
  private router = inject(Router);
  private tenantService = inject(TenantService);

  readonly role = signal<string | null>(this.getUserRole());

  /** Persists the JWT from a successful login/register response and refreshes app state. */
  setSession(token: string) {
    localStorage.setItem('jwt_token', token);
    this.role.set(this.getUserRole());
    this.tenantService.syncFromTenantId(this.getTenantId());
  }

  login(userData: any) {
    return this.httpClient.post(`${environment.gatewayApiUrl}/auth/login`, userData);
  }

  register(userData: any) {
    return this.httpClient.post(`${environment.gatewayApiUrl}/auth/register`, userData);
  }

  updateMe(displayName: string) {
    return this.httpClient.put<UserProfile>(`${environment.gatewayApiUrl}/auth/me`, { displayName });
  }

  getMe() {
    return this.httpClient.get<UserProfile>(`${environment.gatewayApiUrl}/auth/me`);
  }

  listUsers() {
    return this.httpClient.get<UserProfile[]>(`${environment.gatewayApiUrl}/auth/admin/users`);
  }

  setUserStatus(id: string, isActive: boolean) {
    return this.httpClient.put<UserProfile>(`${environment.gatewayApiUrl}/auth/admin/users/${id}/status`, { isActive });
  }

  getUserRole(): string | null {
    if (!this.isAuthenticated()) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    return decoded['role'];
  }

  getUserId(): string | null {
    if (!this.isAuthenticated()) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    return decoded['sub'] ?? null;
  }

  /** Absent for the platform super-admin — everyone else belongs to exactly one medical center. */
  getTenantId(): string | null {
    if (!this.isAuthenticated()) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    return decoded['tenant_id'] ?? null;
  }

  getUserName(): string | null {
    if (!this.isAuthenticated()) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    return decoded['name']
      ?? decoded['unique_name']
      ?? decoded['given_name']
      ?? decoded['email']
      ?? decoded['sub']
      ?? null;
  }

  isAuthenticated(): boolean {
    const decoded = this.getDecodedToken();
    if (!decoded) return false;

    const now = Math.floor(Date.now() / 1000);
    if (now > decoded.exp) {
      this.logout();
      return false;
    }

    return true;
  }

  private getDecodedToken(): any | null {
    const token = localStorage.getItem('jwt_token');
    if (!token) return null;

    try {
      const payload = token.split('.')[1];
      return JSON.parse(atob(payload));
    } catch (e) {
      console.error('Error decoding token', e);
      return null;
    }
  }

  logout() {
    localStorage.removeItem('jwt_token');
    this.role.set(null);
    this.router.navigate(['/login']);
  }
}
