import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { VisitType } from '../appointments/appointments';

export interface DoctorProfile {
  id: string;
  userId: string;
  fullName: string;
  specialization: string;
  city: string;
  description?: string | null;
  isActive: boolean;
  averageRating?: number | null;
  reviewCount?: number;
  priceStationaryCents?: number | null;
  priceOnlineCents?: number | null;
  conditionsTreated?: string[];
  hasPhoto?: boolean;
}

export interface DoctorSearchParams {
  name?: string | null;
  specialization?: string | null;
  city?: string | null;
}

export interface DoctorProfileUpdate {
  fullName: string;
  specialization: string;
  city: string;
  description?: string | null;
  isActive: boolean;
  priceStationaryCents?: number | null;
  priceOnlineCents?: number | null;
  conditionsTreated?: string[];
}

export interface MedicalService {
  id: string;
  doctorProfileId: string;
  name: string;
  description?: string | null;
  priceCents: number;
  allowedVisitTypes: VisitType[];
}

export interface MedicalServiceUpsert {
  name: string;
  description?: string | null;
  priceCents: number;
  allowedVisitTypes: VisitType[];
}

@Injectable({ providedIn: 'root' })
export class DoctorsService {
  private httpClient = inject(HttpClient);
  private baseUrl = `${environment.gatewayApiUrl}/doctors`;

  /** Whether the logged-in doctor has a DoctorProfile yet. null = not checked. */
  readonly hasProfile = signal<boolean | null>(null);

  /** The logged-in doctor's own profile, kept in sync by checkOwnProfile(). */
  readonly ownProfile = signal<DoctorProfile | null>(null);

  search(params: DoctorSearchParams) {
    let httpParams = new HttpParams();
    if (params.name) httpParams = httpParams.set('name', params.name);
    if (params.specialization) httpParams = httpParams.set('specialization', params.specialization);
    if (params.city) httpParams = httpParams.set('city', params.city);

    return this.httpClient.get<DoctorProfile[]>(this.baseUrl, { params: httpParams });
  }

  getById(id: string) {
    return this.httpClient.get<DoctorProfile>(`${this.baseUrl}/${id}`);
  }

  update(id: string, dto: DoctorProfileUpdate) {
    return this.httpClient.put<DoctorProfile>(`${this.baseUrl}/${id}`, dto);
  }

  getMine() {
    return this.httpClient.get<DoctorProfile>(`${this.baseUrl}/me`);
  }

  create(dto: DoctorProfileUpdate) {
    return this.httpClient.post<DoctorProfile>(this.baseUrl, dto);
  }

  /** Fetches the current doctor's profile and keeps `hasProfile` in sync with the result. */
  checkOwnProfile() {
    return this.getMine().pipe(
      tap((profile) => {
        this.hasProfile.set(true);
        this.ownProfile.set(profile);
      }),
      catchError((err: HttpErrorResponse) => {
        if (err.status === 404) {
          this.hasProfile.set(false);
        }
        return throwError(() => err);
      })
    );
  }

  getServices(doctorId: string) {
    return this.httpClient.get<MedicalService[]>(`${this.baseUrl}/${doctorId}/services`);
  }

  addService(doctorId: string, dto: MedicalServiceUpsert) {
    return this.httpClient.post<MedicalService>(`${this.baseUrl}/${doctorId}/services`, dto);
  }

  updateService(doctorId: string, serviceId: string, dto: MedicalServiceUpsert) {
    return this.httpClient.put<MedicalService>(`${this.baseUrl}/${doctorId}/services/${serviceId}`, dto);
  }

  deleteService(doctorId: string, serviceId: string) {
    return this.httpClient.delete<void>(`${this.baseUrl}/${doctorId}/services/${serviceId}`);
  }

  /** Cache-busted so the browser re-fetches after a new photo is uploaded (same URL otherwise). */
  photoUrl(doctorId: string, cacheBust?: number) {
    const bust = cacheBust != null ? `?v=${cacheBust}` : '';
    return `${this.baseUrl}/${doctorId}/photo${bust}`;
  }

  uploadPhoto(doctorId: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.httpClient.put<void>(`${this.baseUrl}/${doctorId}/photo`, formData);
  }

  deletePhoto(doctorId: string) {
    return this.httpClient.delete<void>(`${this.baseUrl}/${doctorId}/photo`);
  }

  /** Unlike search(), this includes inactive/hidden profiles too — for admin moderation. */
  getAllForAdmin() {
    return this.httpClient.get<DoctorProfile[]>(`${this.baseUrl}/admin/all`);
  }

  setActiveStatusAsAdmin(doctorId: string, isActive: boolean) {
    return this.httpClient.put<DoctorProfile>(`${this.baseUrl}/admin/${doctorId}/status`, { isActive });
  }
}
