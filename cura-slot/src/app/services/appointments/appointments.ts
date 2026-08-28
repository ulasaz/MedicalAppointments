import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SlotDto {
  startTime: string;
  endTime: string;
  /** Only populated for working-window entries; absent for booked ranges. */
  allowedVisitTypes?: VisitType[];
}

export interface DayAvailability {
  workingWindows: SlotDto[];
  bookedRanges: SlotDto[];
}

export type VisitType = 'Stationary' | 'Online';

export interface AppointmentCreate {
  doctorId: string;
  startTime: string;
  durationMinutes: number;
  visitType: VisitType;
  medicalServiceId?: string | null;
}

export interface AppointmentInfo {
  id: string;
  doctorId: string;
  doctorName: string;
  patientId: string;
  patientName: string;
  clinicId: string;
  startTime: string;
  endTime: string;
  status: string;
  hasReview: boolean;
  reviewRating?: number | null;
  reviewComment?: string | null;
  visitType: VisitType;
  priceCents: number;
  isPaid: boolean;
  medicalServiceId?: string | null;
  serviceName?: string | null;
}

export interface ReviewCreate {
  rating: number;
  comment?: string | null;
}

export interface DoctorReview {
  id: string;
  rating: number;
  comment?: string | null;
  patientName: string;
  createdAt: string;
  /** Only populated by the admin, cross-doctor listing. */
  doctorName?: string | null;
}

export interface AdminStats {
  totalAppointments: number;
  pendingCount: number;
  confirmedCount: number;
  completedCount: number;
  cancelledCount: number;
  rejectedCount: number;
  totalReviews: number;
  averageRating: number;
}

/** Date as "yyyy-MM-dd". StartTime/EndTime as "HH:mm:ss". */
export interface AvailabilitySlotInfo {
  id: string;
  doctorId: string;
  date: string;
  startTime: string;
  endTime: string;
  allowedVisitTypes: VisitType[];
}

export interface AvailabilitySlotCreate {
  date: string;
  startTime: string;
  endTime: string;
  allowedVisitTypes?: VisitType[];
}

@Injectable({ providedIn: 'root' })
export class AppointmentsService {
  private httpClient = inject(HttpClient);
  private baseUrl = environment.gatewayApiUrl;

  getAvailableSlots(doctorId: string, date: string) {
    return this.httpClient.get<DayAvailability>(`${this.baseUrl}/doctors/${doctorId}/available-slots`, {
      params: { date },
    });
  }

  bookAppointment(dto: AppointmentCreate) {
    return this.httpClient.post<AppointmentInfo>(`${this.baseUrl}/appointments`, dto);
  }

  getMine() {
    return this.httpClient.get<AppointmentInfo[]>(`${this.baseUrl}/appointments/patient`);
  }

  getById(id: string) {
    return this.httpClient.get<AppointmentInfo>(`${this.baseUrl}/appointments/${id}`);
  }

  cancel(id: string) {
    return this.httpClient.post<boolean>(`${this.baseUrl}/appointments/${id}/cancel`, null);
  }

  getForDoctor() {
    return this.httpClient.get<AppointmentInfo[]>(`${this.baseUrl}/appointments/doctor`);
  }

  confirm(id: string) {
    return this.httpClient.post<boolean>(`${this.baseUrl}/appointments/${id}/confirm`, null);
  }

  reject(id: string) {
    return this.httpClient.post<boolean>(`${this.baseUrl}/appointments/${id}/reject`, null);
  }

  getSchedule(doctorId: string) {
    return this.httpClient.get<AvailabilitySlotInfo[]>(`${this.baseUrl}/doctors/${doctorId}/schedule`);
  }

  addScheduleSlot(doctorId: string, dto: AvailabilitySlotCreate) {
    return this.httpClient.post<AvailabilitySlotInfo>(`${this.baseUrl}/doctors/${doctorId}/schedule`, dto);
  }

  deleteScheduleSlot(doctorId: string, slotId: string) {
    return this.httpClient.delete<void>(`${this.baseUrl}/doctors/${doctorId}/schedule/${slotId}`);
  }

  complete(id: string) {
    return this.httpClient.post<boolean>(`${this.baseUrl}/appointments/${id}/complete`, null);
  }

  addReview(appointmentId: string, dto: ReviewCreate) {
    return this.httpClient.post(`${this.baseUrl}/appointments/${appointmentId}/review`, dto);
  }

  getDoctorReviews(doctorId: string) {
    return this.httpClient.get<DoctorReview[]>(`${this.baseUrl}/doctors/${doctorId}/reviews`);
  }

  getAdminStats() {
    return this.httpClient.get<AdminStats>(`${this.baseUrl}/appointments/admin/stats`);
  }

  getAllReviewsForAdmin() {
    return this.httpClient.get<DoctorReview[]>(`${this.baseUrl}/appointments/admin/reviews`);
  }

  deleteReviewAsAdmin(reviewId: string) {
    return this.httpClient.delete<void>(`${this.baseUrl}/appointments/admin/reviews/${reviewId}`);
  }
}
