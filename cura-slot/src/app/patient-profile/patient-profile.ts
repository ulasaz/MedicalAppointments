import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, UserProfile } from '../services/auth/auth';
import { ToastService } from '../services/toast/toast';
import { ProfileAvatarComponent } from '../shared/profile-avatar/profile-avatar';
import { initials } from '../shared/initials';

@Component({
  selector: 'app-patient-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslatePipe, ProfileAvatarComponent],
  templateUrl: './patient-profile.html',
  styleUrl: './patient-profile.css',
})
export class PatientProfilePage implements OnInit {
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  readonly initials = initials;

  profile: UserProfile | null = null;
  isLoading = true;

  isEditingName = false;
  isSavingName = false;
  formDisplayName = '';

  ngOnInit() {
    this.authService.getMe().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  startEditName() {
    if (!this.profile) return;
    this.formDisplayName = this.profile.displayName;
    this.isEditingName = true;
  }

  cancelEditName() {
    this.isEditingName = false;
  }

  saveName() {
    if (!this.formDisplayName.trim()) return;

    this.isSavingName = true;
    this.authService.updateMe(this.formDisplayName.trim()).subscribe({
      next: (updated) => {
        this.profile = updated;
        this.isEditingName = false;
        this.isSavingName = false;
        this.toastService.success(this.translateService.instant('PATIENT_PROFILE.TOAST.SAVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSavingName = false;
        this.toastService.error(this.translateService.instant('PATIENT_PROFILE.TOAST.SAVE_ERROR'));
        this.cdr.markForCheck();
      }
    });
  }
}
