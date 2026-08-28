import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslatePipe } from '@ngx-translate/core';
import { AppointmentInfo, AppointmentsService } from '../services/appointments/appointments';
import { AuthService } from '../services/auth/auth';
import { PricePipe } from '../pipes/price-pipe';
import { DoctorSidebarComponent } from '../doctor-sidebar/doctor-sidebar';
import { statusBadgeClasses } from '../shared/status-badge';

const PAYABLE_STATUSES = ['Pending', 'Confirmed'];

@Component({
  selector: 'app-appointment-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe, PricePipe, DoctorSidebarComponent],
  templateUrl: './appointment-detail.html',
  styleUrl: './appointment-detail.css',
})
export class AppointmentDetailPage implements OnInit {
  private route = inject(ActivatedRoute);
  private appointmentsService = inject(AppointmentsService);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  appointment: AppointmentInfo | null = null;
  isLoading = true;
  notFound = false;
  forbidden = false;

  get isDoctor(): boolean {
    return this.authService.role() === 'Doctor';
  }

  get statusBadgeClasses(): string {
    return this.appointment ? statusBadgeClasses(this.appointment.status) : '';
  }

  get canPayOnline(): boolean {
    return !!this.appointment
      && this.appointment.visitType === 'Online'
      && !this.appointment.isPaid
      && PAYABLE_STATUSES.includes(this.appointment.status);
  }

  get payInOffice(): boolean {
    return !!this.appointment
      && this.appointment.visitType === 'Stationary'
      && PAYABLE_STATUSES.includes(this.appointment.status);
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound = true;
      this.isLoading = false;
      return;
    }

    this.appointmentsService.getById(id).subscribe({
      next: (appointment) => {
        this.appointment = appointment;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 403) {
          this.forbidden = true;
        } else {
          this.notFound = true;
        }
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
