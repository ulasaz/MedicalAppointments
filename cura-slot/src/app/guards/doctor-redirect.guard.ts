import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth/auth';
import { DoctorsService } from '../services/doctors/doctors';

/** Doctors live in their own dashboard, not the patient-facing home/search pages —
 *  keeps a logged-in doctor from landing on marketing or "browse doctors" pages
 *  meant for patients. */
export const doctorHomeRedirectGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.getUserRole() !== 'Doctor') return true;

  router.navigate(['/doctor-dashboard']);
  return false;
};

/** A doctor may view their own public profile page (that's how "My profile" works
 *  for the Doctor role) but not browse other doctors' profiles — only patients do that. */
export const doctorOwnProfileGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const doctorsService = inject(DoctorsService);
  const router = inject(Router);

  if (authService.getUserRole() !== 'Doctor') return true;

  const routeId = route.paramMap.get('id');
  return doctorsService.checkOwnProfile().pipe(
    map((profile) => {
      if (profile.id === routeId) return true;
      router.navigate(['/doctor-dashboard']);
      return false;
    }),
    catchError(() => {
      router.navigate(['/doctor-dashboard']);
      return of(false);
    })
  );
};
