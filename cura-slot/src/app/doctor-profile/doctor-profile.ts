import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DoctorProfile as DoctorProfileModel, DoctorProfileUpdate, DoctorsService, MedicalService } from '../services/doctors/doctors';
import { AppointmentsService, DayAvailability, DoctorReview, VisitType } from '../services/appointments/appointments';
import { AuthService } from '../services/auth/auth';
import { ToastService } from '../services/toast/toast';
import { PricePipe } from '../pipes/price-pipe';
import { DoctorSidebarComponent } from '../doctor-sidebar/doctor-sidebar';
import { ProfileAvatarComponent } from '../shared/profile-avatar/profile-avatar';
import { initials } from '../shared/initials';

interface TimeSlot {
  startIso: string;
  endIso: string;
  isBooked: boolean;
}

type ProfileSection = 'personal' | 'location' | 'bio' | 'conditions';

@Component({
  selector: 'app-doctor-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslatePipe, PricePipe, DoctorSidebarComponent, ProfileAvatarComponent],
  templateUrl: './doctor-profile.html',
  styleUrl: './doctor-profile.css',
})
export class DoctorProfilePage implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private doctorsService = inject(DoctorsService);
  private appointmentsService = inject(AppointmentsService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private sanitizer = inject(DomSanitizer);
  private cdr = inject(ChangeDetectorRef);

  readonly initials = initials;

  doctor: DoctorProfileModel | null = null;
  isLoading = true;
  notFound = false;

  // Per-section inline editing (Personal Info / Location / Bio), each independent —
  // matches the reference design rather than one big all-fields form.
  editingSection: ProfileSection | null = null;
  isSavingSection = false;
  isTogglingActive = false;
  isUploadingPhoto = false;
  // Bumped after every upload/delete so the <img> URL changes and the browser
  // re-fetches instead of serving the previous photo from cache.
  photoCacheBust = Date.now();
  formFullName = '';
  formSpecialization = '';
  formCity = '';
  formDescription = '';
  formConditions: string[] = [];
  newConditionDraft = '';

  // Reviews
  reviews: DoctorReview[] = [];
  isLoadingReviews = false;

  // Custom services offered (read-only for visitors)
  services: MedicalService[] = [];
  isLoadingServices = false;

  // Booking
  dayAvailability: DayAvailability | null = null;
  selectedDate: string | null = null;
  isLoadingSlots = false;
  hasSearchedSlots = false;
  selectedStartIso: string | null = null;
  selectedVisitType: VisitType | null = null;
  // null = a plain visit, priced off the doctor's general Stationary/Online pricing.
  // Set = a specific named service (its own price and allowed visit types apply instead).
  selectedServiceId: string | null = null;
  readonly durationOptions = [15, 30, 45, 60];
  selectedDuration = 30;
  isBooking = false;
  bookingSuccess = false;

  get doctorPhotoUrl(): string | null {
    return this.doctor?.hasPhoto ? this.doctorsService.photoUrl(this.doctor.id, this.photoCacheBust) : null;
  }

  get selectedService(): MedicalService | null {
    return this.services.find(s => s.id === this.selectedServiceId) ?? null;
  }

  get availableVisitTypes(): { type: VisitType; priceCents: number }[] {
    if (!this.doctor) return [];

    if (this.selectedServiceId) {
      const service = this.selectedService;
      return service ? service.allowedVisitTypes.map(type => ({ type, priceCents: service.priceCents })) : [];
    }

    const options: { type: VisitType; priceCents: number }[] = [];
    if (this.doctor.priceStationaryCents != null) {
      options.push({ type: 'Stationary', priceCents: this.doctor.priceStationaryCents });
    }
    if (this.doctor.priceOnlineCents != null) {
      options.push({ type: 'Online', priceCents: this.doctor.priceOnlineCents });
    }
    return options;
  }

  get daySlots(): TimeSlot[] {
    if (!this.dayAvailability) return [];

    const stepMs = this.selectedDuration * 60_000;
    const slots: TimeSlot[] = [];

    for (const window of this.dayAvailability.workingWindows) {
      if (this.selectedVisitType && window.allowedVisitTypes && !window.allowedVisitTypes.includes(this.selectedVisitType)) {
        continue;
      }

      let cursor = new Date(window.startTime).getTime();
      const windowEnd = new Date(window.endTime).getTime();

      while (cursor + stepMs <= windowEnd) {
        const slotEndMs = cursor + stepMs;
        const isBooked = this.dayAvailability.bookedRanges.some(booked => {
          const bookedStart = new Date(booked.startTime).getTime();
          const bookedEnd = new Date(booked.endTime).getTime();
          return bookedStart < slotEndMs && bookedEnd > cursor;
        });

        slots.push({
          startIso: new Date(cursor).toISOString(),
          endIso: new Date(slotEndMs).toISOString(),
          isBooked,
        });

        cursor = slotEndMs;
      }
    }

    return slots;
  }

  get isOwner(): boolean {
    return !!this.doctor
      && this.authService.getUserRole() === 'Doctor'
      && this.authService.getUserId() === this.doctor.userId;
  }

  get isPatient(): boolean {
    return this.authService.role() === 'Patient';
  }

  get isAnonymous(): boolean {
    return !this.authService.isAuthenticated();
  }

  /** Embedded map centered on the doctor's city — there's no street-address field
   *  on DoctorProfile, so this uses the real city rather than a fabricated address. */
  get mapUrl(): SafeResourceUrl {
    const query = encodeURIComponent(`${this.doctor?.city ?? ''}, Poland`);
    return this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.google.com/maps?q=${query}&output=embed`);
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound = true;
      this.isLoading = false;
      return;
    }

    this.doctorsService.getById(id).subscribe({
      next: (doctor) => {
        this.doctor = doctor;
        this.isLoading = false;
        this.loadReviews(doctor.id);
        this.loadServices(doctor.id);
        if (this.availableVisitTypes.length === 1) {
          this.selectedVisitType = this.availableVisitTypes[0].type;
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.notFound = true;
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private loadServices(doctorId: string) {
    this.isLoadingServices = true;
    this.doctorsService.getServices(doctorId).subscribe({
      next: (services) => {
        this.services = services;
        this.isLoadingServices = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingServices = false;
        this.cdr.markForCheck();
      }
    });
  }

  private loadReviews(doctorId: string) {
    this.isLoadingReviews = true;
    this.appointmentsService.getDoctorReviews(doctorId).subscribe({
      next: (reviews) => {
        this.reviews = reviews;
        this.isLoadingReviews = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingReviews = false;
        this.cdr.markForCheck();
      }
    });
  }

  startEditSection(section: ProfileSection) {
    if (!this.doctor) return;
    this.formFullName = this.doctor.fullName;
    this.formSpecialization = this.doctor.specialization;
    this.formCity = this.doctor.city;
    this.formDescription = this.doctor.description ?? '';
    this.formConditions = [...(this.doctor.conditionsTreated ?? [])];
    this.newConditionDraft = '';
    this.editingSection = section;
  }

  cancelEditSection() {
    this.editingSection = null;
  }

  addConditionFromDraft() {
    const value = this.newConditionDraft.trim();
    if (!value || this.formConditions.includes(value)) return;
    this.formConditions = [...this.formConditions, value];
    this.newConditionDraft = '';
  }

  removeConditionDraft(condition: string) {
    this.formConditions = this.formConditions.filter(c => c !== condition);
  }

  saveSection() {
    if (!this.doctor || !this.editingSection) return;

    const dto: DoctorProfileUpdate = {
      fullName: this.editingSection === 'personal' ? this.formFullName : this.doctor.fullName,
      specialization: this.editingSection === 'personal' ? this.formSpecialization : this.doctor.specialization,
      city: this.editingSection === 'location' ? this.formCity : this.doctor.city,
      description: this.editingSection === 'bio' ? this.formDescription : this.doctor.description,
      isActive: this.doctor.isActive,
      priceStationaryCents: this.doctor.priceStationaryCents,
      priceOnlineCents: this.doctor.priceOnlineCents,
      conditionsTreated: this.editingSection === 'conditions' ? this.formConditions : (this.doctor.conditionsTreated ?? []),
    };

    this.isSavingSection = true;
    this.doctorsService.update(this.doctor.id, dto).subscribe({
      next: (updated) => {
        this.doctor = updated;
        this.editingSection = null;
        this.isSavingSection = false;
        this.toastService.success(this.translateService.instant('DOCTOR_PROFILE.TOAST.SAVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSavingSection = false;
        this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.TOAST.SAVE_ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  toggleActive() {
    if (!this.doctor) return;

    const dto: DoctorProfileUpdate = {
      fullName: this.doctor.fullName,
      specialization: this.doctor.specialization,
      city: this.doctor.city,
      description: this.doctor.description,
      isActive: !this.doctor.isActive,
      priceStationaryCents: this.doctor.priceStationaryCents,
      priceOnlineCents: this.doctor.priceOnlineCents,
      conditionsTreated: this.doctor.conditionsTreated ?? [],
    };

    this.isTogglingActive = true;
    this.doctorsService.update(this.doctor.id, dto).subscribe({
      next: (updated) => {
        this.doctor = updated;
        this.isTogglingActive = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isTogglingActive = false;
        this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.TOAST.SAVE_ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  onPhotoSelected(file: File) {
    if (!this.doctor) return;

    this.isUploadingPhoto = true;
    this.doctorsService.uploadPhoto(this.doctor.id, file).subscribe({
      next: () => {
        this.doctor = this.doctor ? { ...this.doctor, hasPhoto: true } : this.doctor;
        this.photoCacheBust = Date.now();
        this.isUploadingPhoto = false;
        this.toastService.success(this.translateService.instant('DOCTOR_PROFILE.TOAST.PHOTO_SAVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isUploadingPhoto = false;
        this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.TOAST.PHOTO_ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  onPhotoRemoved() {
    if (!this.doctor) return;

    this.isUploadingPhoto = true;
    this.doctorsService.deletePhoto(this.doctor.id).subscribe({
      next: () => {
        this.doctor = this.doctor ? { ...this.doctor, hasPhoto: false } : this.doctor;
        this.isUploadingPhoto = false;
        this.toastService.success(this.translateService.instant('DOCTOR_PROFILE.TOAST.PHOTO_REMOVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isUploadingPhoto = false;
        this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.TOAST.PHOTO_ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  onDateChange(event: Event) {
    const date = (event.target as HTMLInputElement).value;
    this.selectedDate = date || null;
    this.selectedStartIso = null;
    this.bookingSuccess = false;
    this.dayAvailability = null;

    if (!this.doctor || !date) {
      this.hasSearchedSlots = false;
      return;
    }

    this.loadDayAvailability();
  }

  private loadDayAvailability() {
    if (!this.doctor || !this.selectedDate) return;

    this.isLoadingSlots = true;
    this.appointmentsService.getAvailableSlots(this.doctor.id, this.selectedDate).subscribe({
      next: (dayAvailability) => {
        this.dayAvailability = dayAvailability;
        this.isLoadingSlots = false;
        this.hasSearchedSlots = true;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingSlots = false;
        this.hasSearchedSlots = true;
        this.cdr.markForCheck();
      }
    });
  }

  selectService(serviceId: string | null) {
    if (this.selectedServiceId === serviceId) return;

    this.selectedServiceId = serviceId;
    this.selectedVisitType = null;
    this.selectedDate = null;
    this.dayAvailability = null;
    this.hasSearchedSlots = false;
    this.selectedStartIso = null;
    this.bookingSuccess = false;

    if (this.availableVisitTypes.length === 1) {
      this.selectedVisitType = this.availableVisitTypes[0].type;
    }
  }

  selectVisitType(type: VisitType) {
    this.selectedVisitType = type;
    this.selectedStartIso = null;
    this.bookingSuccess = false;
  }

  selectDuration(duration: number) {
    this.selectedDuration = duration;
    this.selectedStartIso = null;
    this.bookingSuccess = false;
  }

  selectStartTime(slot: TimeSlot) {
    if (slot.isBooked) return;
    this.selectedStartIso = slot.startIso;
    this.bookingSuccess = false;
  }

  bookAppointment() {
    if (!this.doctor || !this.selectedStartIso || !this.selectedVisitType) return;

    this.isBooking = true;

    this.appointmentsService.bookAppointment({
      doctorId: this.doctor.id,
      startTime: this.selectedStartIso,
      durationMinutes: this.selectedDuration,
      visitType: this.selectedVisitType,
      medicalServiceId: this.selectedServiceId,
    }).subscribe({
      next: (appointment) => {
        this.isBooking = false;
        this.bookingSuccess = true;
        this.selectedStartIso = null;
        this.cdr.markForCheck();
        this.router.navigate(['/appointments', appointment.id]);
      },
      error: (err: HttpErrorResponse) => {
        this.isBooking = false;

        if (err.status === 400) {
          this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.BOOKING.ERROR_INVALID'));
        } else if (err.status === 409) {
          // Someone else just booked this slot — resync the grid instead of leaving a stale
          // selection the patient could retry into the same 409.
          this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.BOOKING.ERROR_CONFLICT'));
          this.selectedStartIso = null;
          this.loadDayAvailability();
        } else {
          this.toastService.error(this.translateService.instant('DOCTOR_PROFILE.BOOKING.ERROR_GENERIC'));
        }
        this.cdr.markForCheck();
      }
    });
  }
}
