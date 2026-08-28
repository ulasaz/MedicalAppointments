import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AppointmentInfo, AppointmentsService } from '../services/appointments/appointments';
import { ToastService } from '../services/toast/toast';
import { PricePipe } from '../pipes/price-pipe';
import { statusBadgeClasses } from '../shared/status-badge';
import { initials } from '../shared/initials';

const CANCELLABLE_STATUSES = ['Pending', 'Confirmed'];
const RESOLVED_STATUSES = ['Completed', 'Cancelled', 'Rejected'];

type Tab = 'upcoming' | 'past';

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslatePipe, PricePipe],
  templateUrl: './appointments.html',
  styleUrl: './appointments.css',
})
export class Appointments implements OnInit {
  private appointmentsService = inject(AppointmentsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  readonly initials = initials;
  readonly statusBadgeClasses = statusBadgeClasses;

  appointments: AppointmentInfo[] = [];
  isLoading = true;
  cancellingId: string | null = null;
  activeTab: Tab = 'upcoming';

  reviewingAppointmentId: string | null = null;
  reviewRating = 5;
  reviewComment = '';
  isSubmittingReview = false;

  get upcoming(): AppointmentInfo[] {
    return this.appointments
      .filter(a => !RESOLVED_STATUSES.includes(a.status))
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
  }

  get past(): AppointmentInfo[] {
    return this.appointments
      .filter(a => RESOLVED_STATUSES.includes(a.status))
      .sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime());
  }

  get visibleAppointments(): AppointmentInfo[] {
    return this.activeTab === 'upcoming' ? this.upcoming : this.past;
  }

  selectTab(tab: Tab) {
    this.activeTab = tab;
  }

  ngOnInit() {
    this.loadAppointments();
  }

  loadAppointments() {
    this.isLoading = true;
    this.appointmentsService.getMine().subscribe({
      next: (appointments) => {
        this.appointments = appointments;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  isCancellable(appointment: AppointmentInfo): boolean {
    return CANCELLABLE_STATUSES.includes(appointment.status);
  }

  cancel(appointment: AppointmentInfo) {
    this.cancellingId = appointment.id;
    this.appointmentsService.cancel(appointment.id).subscribe({
      next: () => {
        appointment.status = 'Cancelled';
        this.cancellingId = null;
        this.toastService.success(this.translateService.instant('APPOINTMENTS.TOAST.CANCELLED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        this.cancellingId = null;

        if (err.status === 409) {
          // Someone/something already changed this appointment's state (e.g. a double-cancel race,
          // or the doctor rejected it in the meantime) — resync from the server instead of letting
          // the user retry into the same 409.
          this.toastService.error(this.translateService.instant('APPOINTMENTS.TOAST.CANCEL_CONFLICT'));
          this.loadAppointments();
        } else {
          this.toastService.error(this.translateService.instant('APPOINTMENTS.TOAST.CANCEL_ERROR'));
        }
        this.cdr.markForCheck();
      }
    });
  }

  startReview(appointment: AppointmentInfo) {
    this.reviewingAppointmentId = appointment.id;
    this.reviewRating = 5;
    this.reviewComment = '';
  }

  cancelReview() {
    this.reviewingAppointmentId = null;
  }

  submitReview(appointment: AppointmentInfo) {
    this.isSubmittingReview = true;
    this.appointmentsService.addReview(appointment.id, {
      rating: this.reviewRating,
      comment: this.reviewComment || null,
    }).subscribe({
      next: () => {
        appointment.hasReview = true;
        this.reviewingAppointmentId = null;
        this.isSubmittingReview = false;
        this.toastService.success(this.translateService.instant('APPOINTMENTS.REVIEW.TOAST.SUBMITTED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmittingReview = false;

        if (err.status === 409) {
          // Already reviewed (e.g. a duplicate tab) — resync the flag instead of letting the user retry into the same 409.
          appointment.hasReview = true;
          this.reviewingAppointmentId = null;
          this.toastService.error(this.translateService.instant('APPOINTMENTS.REVIEW.ERROR_CONFLICT'));
        } else {
          this.toastService.error(this.translateService.instant('APPOINTMENTS.REVIEW.ERROR_GENERIC'));
        }
        this.cdr.markForCheck();
      }
    });
  }
}
