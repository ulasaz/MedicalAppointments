import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth/auth';

export const roleGuard = (allowedRole: string): CanActivateFn => {
  
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);
    
    const userRole = authService.getUserRole();

    if (userRole === allowedRole) {
      return true;
    }

    console.warn(`Acces denied: ${allowedRole}`);
    router.navigate(['/login']);
    return false;
  };
  
};