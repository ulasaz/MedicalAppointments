import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { LoginCustomerComponent } from './login/customer/login-customer.component';
import { RegisterCustomerComponent } from './register/register-customer-component/register-customer-component';
import { Doctors } from './doctors/doctors';
import { HomePage } from './home/home';
import { DoctorProfilePage } from './doctor-profile/doctor-profile';
import { Appointments } from './appointments/appointments';
import { AppointmentDetailPage } from './appointment-detail/appointment-detail';
import { CheckoutComponent } from './payment/checkout.component';
import { PaymentSuccessPage } from './payment-success/payment-success';
import { DoctorDashboard } from './doctor-dashboard/doctor-dashboard';
import { DoctorProfileSetupPage } from './doctor-profile-setup/doctor-profile-setup';
import { DoctorSchedulePage } from './doctor-schedule/doctor-schedule';
import { PatientProfilePage } from './patient-profile/patient-profile';
import { AdminPage } from './admin/admin';
import { AuthService } from './services/auth/auth';
import { TenantService } from './services/tenant/tenant';
import { roleGuard } from './guards/role.guard';
import { doctorHomeRedirectGuard, doctorOwnProfileGuard } from './guards/doctor-redirect.guard';

export const routes: Routes = [{
    // Shareable, per-medical-center entry link (e.g. /c/green-valley-clinic) — sets that
    // center as the active tenant for anonymous browsing, then drops the visitor straight
    // into its doctor list. Falls through to whatever tenant was already selected if the
    // slug doesn't match any known center, rather than erroring.
    path: 'c/:slug',
    redirectTo: (redirectData) => {
      const tenantService = inject(TenantService);
      const slug = redirectData.paramMap.get('slug');
      const center = tenantService.centers().find((c) => c.slug === slug);
      if (center) {
        tenantService.selectTenant(center.id);
      }
      return '/doctors/search';
    }
  },
  {
    path: 'login',
    component: LoginCustomerComponent
  },
  {
    path: 'register',
    component: RegisterCustomerComponent
  },
  {
    path: 'main',
    component: HomePage,
    canActivate: [doctorHomeRedirectGuard]
  },
  {
    path: 'doctors/search',
    component: Doctors,
    canActivate: [doctorHomeRedirectGuard]
  },
  {
    path: 'doctors/:id',
    component: DoctorProfilePage,
    canActivate: [doctorOwnProfileGuard]
  },
  {
    path: 'appointments',
    component: Appointments,
    canActivate: [roleGuard('Patient')]
  },
  {
    path: 'appointments/:id/pay',
    component: CheckoutComponent,
    canActivate: [roleGuard('Patient')]
  },
  {
    path: 'appointments/:id/payment-success',
    component: PaymentSuccessPage,
    canActivate: [roleGuard('Patient')]
  },
  {
    path: 'appointments/:id',
    component: AppointmentDetailPage
  },
  {
    path: 'patient-profile',
    component: PatientProfilePage,
    canActivate: [roleGuard('Patient')]
  },
  {
    path: 'doctor-dashboard',
    component: DoctorDashboard,
    canActivate: [roleGuard('Doctor')]
  },
  {
    path: 'doctor-setup',
    component: DoctorProfileSetupPage,
    canActivate: [roleGuard('Doctor')]
  },
  {
    path: 'doctor-schedule',
    component: DoctorSchedulePage,
    canActivate: [roleGuard('Doctor')]
  },
  {
    path: 'admin',
    component: AdminPage,
    canActivate: [roleGuard('Admin')]
  },
  {
    path: '',
    redirectTo: () => {
      const auth = inject(AuthService);
      if (!auth.isAuthenticated()) return 'login';
      const role = auth.getUserRole();
      if (role === 'Doctor') return 'doctor-dashboard';
      if (role === 'Admin') return 'admin';
      return 'main';
    },
    pathMatch: 'full'
  }];
