import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private httpClient = inject(HttpClient);
  private router = inject(Router);

  login(userData: any) {
    const gatewayUrl = 'http://localhost:5000/api/auth/login'; 
    
    return this.httpClient.post(gatewayUrl, userData);
  }

  register(userData: any) {
    const gatewayUrl = 'http://localhost:5000/api/auth/register'; 
    
    return this.httpClient.post(gatewayUrl, userData);
  }

  getUserRole(): string | null {
    if (!this.isAuthenticated()) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    return decoded['role'];
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
    localStorage.removeItem('cart');
    this.router.navigate(['/login']);
  }
}
