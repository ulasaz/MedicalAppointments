import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth/auth';
import { ToastService } from '../../services/toast/toast';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { SplineBackgroundComponent } from '../../shared/spline-background/spline-background.component';
import { TenantService } from '../../services/tenant/tenant';
import { CenterBadgeComponent } from '../../shared/center-badge/center-badge.component';

@Component({
  selector: 'app-register-customer-component',
  imports: [ReactiveFormsModule, CommonModule, RouterLink, TranslatePipe, SplineBackgroundComponent, CenterBadgeComponent],
  templateUrl: './register-customer-component.html',
  styleUrl: './register-customer-component.css',
})
export class RegisterCustomerComponent {

  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private translateService = inject(TranslateService);
  tenantService = inject(TenantService);

  profileForm = new FormGroup({
    displayName: new FormControl('', [Validators.required, Validators.minLength(2)]),
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required, Validators.minLength(6)]),
    role: new FormControl<'Patient' | 'Doctor'>('Patient', [Validators.required]),
  });

  showPassword = signal(false);

  setRole(role: 'Patient' | 'Doctor') {
    this.profileForm.patchValue({ role });
  }

  onRegister() {
    if (!this.profileForm.valid) return;

    const tenantId = this.tenantService.selectedTenantId();
    if (!tenantId) return;

    this.authService.register({ ...this.profileForm.value, tenantId }).subscribe({
      next: (response: any) => {
        this.authService.setSession(response.token);
        this.toastService.success(this.translateService.instant('REGISTER.SUCCESS'));
        this.router.navigate(['/']);
      },
      error: () => {
        this.toastService.error(this.translateService.instant('REGISTER.ERROR'));
      }
    });
  }
}
