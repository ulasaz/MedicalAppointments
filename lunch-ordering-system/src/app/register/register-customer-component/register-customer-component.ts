import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth/auth';
import { ToastService } from '../../services/toast/toast';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-register-customer-component',
  imports: [ReactiveFormsModule, CommonModule, RouterLink, TranslatePipe],
  templateUrl: './register-customer-component.html',
  styleUrl: './register-customer-component.css',
})
export class RegisterCustomerComponent {

  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private translateService = inject(TranslateService);

  profileForm = new FormGroup({
    displayName: new FormControl('', [Validators.required, Validators.minLength(2)]),
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required, Validators.minLength(6)]),
  });

  onRegister() {
    if (!this.profileForm.valid) return;

    this.authService.register(this.profileForm.value).subscribe({
      next: (response: any) => {
        localStorage.setItem('jwt_token', response.token);
        this.toastService.success(this.translateService.instant('REGISTER.SUCCESS'));
        this.router.navigate(['/menu']);
      },
      error: () => {
        this.toastService.error(this.translateService.instant('REGISTER.ERROR'));
      }
    });
  }
}
