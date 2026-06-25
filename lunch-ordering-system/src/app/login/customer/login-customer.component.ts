import { Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth/auth';
import { ToastService } from '../../services/toast/toast';
import { TranslateService, TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, RouterLink, TranslateModule],
  templateUrl: './login-customer.component.html',
  styleUrl: './login-customer.component.css'
})
export class LoginCustomerComponent {

  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private translateService = inject(TranslateService);

  activeTab: 'customer' | 'cafe' = 'customer';

  profileForm = new FormGroup({
    email: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required])
  });

  setTab(tab: 'customer' | 'cafe') {
    if (this.activeTab === tab) return;
    this.activeTab = tab;
    this.profileForm.reset();
  }

  onLogin() {
    if (!this.profileForm.valid) return;

    this.authService.login(this.profileForm.value).subscribe({
      next: (response: any) => {
        localStorage.setItem('jwt_token', response.token);

        const role = this.authService.getUserRole();
        const expectedRole = this.activeTab === 'customer' ? 'Customer' : 'Cafe';

        if (role !== expectedRole) {
          localStorage.removeItem('jwt_token');
          this.toastService.error(this.translateService.instant('LOGIN.ERROR_ROLE', { role: expectedRole }));
          return;
        }

        this.toastService.success(this.translateService.instant('LOGIN.SUCCESS'));
        this.router.navigate([this.activeTab === 'customer' ? '/menu' : '/cafe-panel']);
      },
      error: () => {
        this.toastService.error(this.translateService.instant('LOGIN.ERROR_CREDENTIALS'));
      }
    });
  }
}
