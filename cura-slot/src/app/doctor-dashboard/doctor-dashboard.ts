import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AppointmentInfo, AppointmentsService, DoctorReview } from '../services/appointments/appointments';
import { DoctorsService } from '../services/doctors/doctors';
import { ToastService } from '../services/toast/toast';
import { PricePipe } from '../pipes/price-pipe';
import { initials } from '../shared/initials';
import { DoctorSidebarComponent } from '../doctor-sidebar/doctor-sidebar';

const HISTORY_STATUSES = ['Completed', 'Cancelled', 'Rejected'];

type ReviewBucket = 'Excellent' | 'Great' | 'Good' | 'Average';
const REVIEW_BUCKETS: ReviewBucket[] = ['Excellent', 'Great', 'Good', 'Average'];

interface CalendarCell {
  day: number | null;
  isToday: boolean;
  hasAppointment: boolean;
}

@Component({
  selector: 'app-doctor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe, PricePipe, DoctorSidebarComponent],
  templateUrl: './doctor-dashboard.html',
  styleUrl: './doctor-dashboard.css',
})
export class DoctorDashboard implements OnInit {
  private appointmentsService = inject(AppointmentsService);
  doctorsService = inject(DoctorsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  appointments: AppointmentInfo[] = [];
  reviews: DoctorReview[] = [];
  isLoading = true;
  hasProfile: boolean | null = null;
  processingId: string | null = null;

  /** Month currently shown in the calendar widget, relative to the current month. */
  calendarMonthOffset = 0;

  get pending(): AppointmentInfo[] {
    return this.appointments
      .filter(a => a.status === 'Pending')
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
  }

  get confirmed(): AppointmentInfo[] {
    return this.appointments
      .filter(a => a.status === 'Confirmed')
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
  }

  get history(): AppointmentInfo[] {
    return this.appointments
      .filter(a => HISTORY_STATUSES.includes(a.status))
      .sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime());
  }

  get todayAppointments(): AppointmentInfo[] {
    const todayKey = new Date().toDateString();
    return this.appointments
      .filter(a => new Date(a.startTime).toDateString() === todayKey)
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
  }

  get totalPatientsCount(): number {
    return new Set(this.appointments.map(a => a.patientId)).size;
  }

  get todayPatientsCount(): number {
    return new Set(this.todayAppointments.map(a => a.patientId)).size;
  }

  /** Soonest confirmed appointment still ahead of now — shown as "Next Patient". */
  get nextAppointment(): AppointmentInfo | null {
    const now = Date.now();
    const upcoming = this.confirmed.filter(a => new Date(a.startTime).getTime() >= now);
    return upcoming[0] ?? null;
  }

  get newPatientsCount(): number {
    const visitsByPatient = new Map<string, number>();
    for (const a of this.appointments) {
      visitsByPatient.set(a.patientId, (visitsByPatient.get(a.patientId) ?? 0) + 1);
    }
    return [...visitsByPatient.values()].filter(count => count === 1).length;
  }

  get returningPatientsCount(): number {
    return this.totalPatientsCount - this.newPatientsCount;
  }

  /** conic-gradient stops for the new-vs-returning donut; teal for new, rose for returning. */
  get patientDonutBackground(): string {
    const total = this.totalPatientsCount;
    if (total === 0) return 'conic-gradient(#e5e7eb 0% 100%)';
    const newPct = (this.newPatientsCount / total) * 100;
    return `conic-gradient(#2dd4bf 0% ${newPct}%, #fb7185 ${newPct}% 100%)`;
  }

  get reviewBuckets(): { label: ReviewBucket; count: number; pct: number }[] {
    const total = this.reviews.length;
    const counts: Record<ReviewBucket, number> = { Excellent: 0, Great: 0, Good: 0, Average: 0 };
    for (const review of this.reviews) {
      if (review.rating >= 5) counts.Excellent++;
      else if (review.rating === 4) counts.Great++;
      else if (review.rating === 3) counts.Good++;
      else counts.Average++;
    }
    return REVIEW_BUCKETS.map(label => ({
      label,
      count: counts[label],
      pct: total === 0 ? 0 : Math.round((counts[label] / total) * 100),
    }));
  }

  get calendarLabel(): string {
    const date = this.calendarMonthDate();
    const locale = this.translateService.currentLang === 'pl' ? 'pl-PL' : 'en-US';
    return date.toLocaleDateString(locale, { month: 'long', year: 'numeric' });
  }

  get calendarWeekdays(): string[] {
    const locale = this.translateService.currentLang === 'pl' ? 'pl-PL' : 'en-US';
    const base = new Date(2024, 0, 1); // a Monday
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(base);
      d.setDate(base.getDate() + i);
      return d.toLocaleDateString(locale, { weekday: 'short' }).slice(0, 2);
    });
  }

  get calendarCells(): CalendarCell[] {
    const monthDate = this.calendarMonthDate();
    const year = monthDate.getFullYear();
    const month = monthDate.getMonth();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    // Convert JS's Sunday-first getDay() to a Monday-first offset.
    const firstWeekday = (new Date(year, month, 1).getDay() + 6) % 7;

    const appointmentDays = new Set(
      this.appointments
        .filter(a => {
          const d = new Date(a.startTime);
          return d.getFullYear() === year && d.getMonth() === month;
        })
        .map(a => new Date(a.startTime).getDate())
    );

    const today = new Date();
    const isCurrentMonth = today.getFullYear() === year && today.getMonth() === month;

    const cells: CalendarCell[] = Array.from({ length: firstWeekday }, () => ({ day: null, isToday: false, hasAppointment: false }));
    for (let day = 1; day <= daysInMonth; day++) {
      cells.push({
        day,
        isToday: isCurrentMonth && today.getDate() === day,
        hasAppointment: appointmentDays.has(day),
      });
    }
    return cells;
  }

  shiftCalendarMonth(delta: number) {
    this.calendarMonthOffset += delta;
  }

  private calendarMonthDate(): Date {
    const d = new Date();
    d.setDate(1);
    d.setMonth(d.getMonth() + this.calendarMonthOffset);
    return d;
  }

  ngOnInit() {
    this.doctorsService.checkOwnProfile().subscribe({
      next: (profile) => {
        this.hasProfile = true;
        this.loadAppointments();
        this.appointmentsService.getDoctorReviews(profile.id).subscribe({
          next: (reviews) => {
            this.reviews = reviews;
            this.cdr.markForCheck();
          },
          error: () => {},
        });
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 404) {
          this.hasProfile = false;
          this.isLoading = false;
          this.cdr.markForCheck();
        } else {
          // Transient error checking profile status — don't block the dashboard on it.
          this.hasProfile = true;
          this.loadAppointments();
        }
      }
    });
  }

  loadAppointments() {
    this.isLoading = true;
    this.appointmentsService.getForDoctor().subscribe({
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

  readonly initials = initials;

  confirm(appointment: AppointmentInfo) {
    this.processingId = appointment.id;
    this.appointmentsService.confirm(appointment.id).subscribe({
      next: () => {
        appointment.status = 'Confirmed';
        this.processingId = null;
        this.toastService.success(this.translateService.instant('DOCTOR_DASHBOARD.TOAST.CONFIRMED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => this.handleActionError(err)
    });
  }

  reject(appointment: AppointmentInfo) {
    this.processingId = appointment.id;
    this.appointmentsService.reject(appointment.id).subscribe({
      next: () => {
        appointment.status = 'Rejected';
        this.processingId = null;
        this.toastService.success(this.translateService.instant('DOCTOR_DASHBOARD.TOAST.REJECTED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => this.handleActionError(err)
    });
  }

  isCompletable(appointment: AppointmentInfo): boolean {
    return appointment.status === 'Confirmed';
  }

  complete(appointment: AppointmentInfo) {
    this.processingId = appointment.id;
    this.appointmentsService.complete(appointment.id).subscribe({
      next: () => {
        appointment.status = 'Completed';
        this.processingId = null;
        this.toastService.success(this.translateService.instant('DOCTOR_DASHBOARD.TOAST.COMPLETED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => this.handleActionError(err)
    });
  }

  private handleActionError(err: HttpErrorResponse) {
    this.processingId = null;

    if (err.status === 409) {
      // The appointment's state already changed server-side (e.g. the patient cancelled it in the
      // meantime) — resync instead of leaving stale buttons the doctor could retry into the same 409.
      this.toastService.error(this.translateService.instant('DOCTOR_DASHBOARD.TOAST.CONFLICT'));
      this.loadAppointments();
    } else {
      this.toastService.error(this.translateService.instant('DOCTOR_DASHBOARD.TOAST.ACTION_ERROR'));
    }
    this.cdr.markForCheck();
  }
}
