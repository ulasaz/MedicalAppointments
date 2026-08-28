import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth/auth';
import { DoctorsService } from '../../services/doctors/doctors';
import { ToastService } from '../../services/toast/toast';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SplineBackgroundComponent } from '../../shared/spline-background/spline-background.component';
import { CenterBadgeComponent } from '../../shared/center-badge/center-badge.component';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, RouterLink, TranslateModule, SplineBackgroundComponent, CenterBadgeComponent],
  templateUrl: './login-customer.component.html',
  styleUrl: './login-customer.component.css'
})
export class LoginCustomerComponent {

  private authService = inject(AuthService);
  private doctorsService = inject(DoctorsService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private translateService = inject(TranslateService);

  profileForm = new FormGroup({
    email: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required])
  });

  showPassword = signal(false);

  onLogin() {
    if (!this.profileForm.valid) return;

    this.authService.login(this.profileForm.value).subscribe({
      next: (response: any) => {
        this.authService.setSession(response.token);
        this.toastService.success(this.translateService.instant('LOGIN.SUCCESS'));

        if (this.authService.getUserRole() === 'Doctor') {
          this.doctorsService.checkOwnProfile().subscribe({
            next: () => this.router.navigate(['/']),
            error: (err: HttpErrorResponse) => {
              this.router.navigate([err.status === 404 ? '/doctor-setup' : '/']);
            }
          });
        } else {
          this.router.navigate(['/']);
        }
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 403) {
          this.toastService.error(this.translateService.instant('LOGIN.ERROR_DEACTIVATED'));
        } else {
          this.toastService.error(this.translateService.instant('LOGIN.ERROR_CREDENTIALS'));
        }
      }
    });
  }
}
