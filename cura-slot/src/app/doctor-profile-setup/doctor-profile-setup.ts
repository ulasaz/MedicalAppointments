import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DoctorsService } from '../services/doctors/doctors';
import { ToastService } from '../services/toast/toast';
import { DoctorProfileFormComponent, DoctorProfileFormValue } from '../shared/doctor-profile-form/doctor-profile-form';
import { DoctorSidebarComponent } from '../doctor-sidebar/doctor-sidebar';

@Component({
  selector: 'app-doctor-profile-setup',
  standalone: true,
  imports: [CommonModule, TranslatePipe, DoctorProfileFormComponent, DoctorSidebarComponent],
  templateUrl: './doctor-profile-setup.html',
})
export class DoctorProfileSetupPage {
  private doctorsService = inject(DoctorsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  isSaving = false;

  create(value: DoctorProfileFormValue) {
    this.isSaving = true;

    this.doctorsService.create(value).subscribe({
      next: (created) => {
        this.isSaving = false;
        this.doctorsService.hasProfile.set(true);
        this.toastService.success(this.translateService.instant('DOCTOR_PROFILE_SETUP.SUCCESS'));
        this.router.navigate(['/doctors', created.id]);
      },
      error: (err: HttpErrorResponse) => {
        this.isSaving = false;

        if (err.status === 409) {
          this.doctorsService.hasProfile.set(true);
          this.toastService.error(this.translateService.instant('DOCTOR_PROFILE_SETUP.ERROR_CONFLICT'));
          this.router.navigate(['/doctor-dashboard']);
        } else {
          this.toastService.error(this.translateService.instant('DOCTOR_PROFILE_SETUP.ERROR_GENERIC'));
        }
        this.cdr.markForCheck();
      }
    });
  }
}
