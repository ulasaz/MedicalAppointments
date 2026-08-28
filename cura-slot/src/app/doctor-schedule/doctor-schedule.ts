import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DoctorProfile, DoctorsService, MedicalService } from '../services/doctors/doctors';
import { AppointmentsService, AvailabilitySlotInfo, VisitType } from '../services/appointments/appointments';
import { ToastService } from '../services/toast/toast';
import { DoctorSidebarComponent } from '../doctor-sidebar/doctor-sidebar';
import { PricePipe } from '../pipes/price-pipe';

interface CalendarDay {
  date: Date;
  iso: string;
  inCurrentMonth: boolean;
  isPast: boolean;
  isToday: boolean;
  count: number;
}

interface Treatment {
  type: VisitType;
  priceCents: number;
}

const ALL_VISIT_TYPES: VisitType[] = ['Stationary', 'Online'];

function toIso(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-doctor-schedule',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, DoctorSidebarComponent, PricePipe],
  templateUrl: './doctor-schedule.html',
  styleUrl: './doctor-schedule.css',
})
export class DoctorSchedulePage implements OnInit {
  private doctorsService = inject(DoctorsService);
  private appointmentsService = inject(AppointmentsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  readonly weekdayLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly allVisitTypes = ALL_VISIT_TYPES;

  doctorId: string | null = null;
  doctor: DoctorProfile | null = null;
  isLoading = true;
  schedule: AvailabilitySlotInfo[] = [];

  currentMonth = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
  selectedDate: string | null = null;

  newSlotStart = '09:00';
  newSlotEnd = '17:00';
  newSlotVisitTypes: VisitType[] = ['Stationary', 'Online'];
  isAddingSlot = false;
  deletingSlotId: string | null = null;

  // Treatments (the two visit types a doctor can offer, priced independently on
  // DoctorProfile — there's no separate "treatment" entity in the domain model).
  isAddingTreatment = false;
  newTreatmentType: VisitType | null = null;
  newTreatmentPrice: number | null = null;
  isSavingTreatment = false;
  removingType: VisitType | null = null;

  // Custom services (doctor-defined offerings like "Kwalifikacja do operacji"
  // or a specific lab analysis), each with its own online/stationary/both modality.
  // The same inline form is reused for both adding and editing — editingServiceId
  // is null while adding, set to the service's id while editing.
  services: MedicalService[] = [];
  isLoadingServices = false;
  isAddingService = false;
  editingServiceId: string | null = null;
  newServiceName = '';
  newServiceDescription = '';
  newServicePrice: number | null = null;
  newServiceVisitTypes: VisitType[] = ['Stationary', 'Online'];
  isSavingService = false;
  deletingServiceId: string | null = null;

  get treatments(): Treatment[] {
    if (!this.doctor) return [];
    const list: Treatment[] = [];
    if (this.doctor.priceStationaryCents != null) list.push({ type: 'Stationary', priceCents: this.doctor.priceStationaryCents });
    if (this.doctor.priceOnlineCents != null) list.push({ type: 'Online', priceCents: this.doctor.priceOnlineCents });
    return list;
  }

  get missingVisitTypes(): VisitType[] {
    const configured = new Set(this.treatments.map(t => t.type));
    return ALL_VISIT_TYPES.filter(type => !configured.has(type));
  }

  get calendarDays(): CalendarDay[] {
    const year = this.currentMonth.getFullYear();
    const month = this.currentMonth.getMonth();
    const firstOfMonth = new Date(year, month, 1);
    const firstWeekday = (firstOfMonth.getDay() + 6) % 7; // 0 = Monday
    const gridStart = new Date(year, month, 1 - firstWeekday);
    const todayIso = toIso(new Date());
    const counts = this.countsByDate;

    const days: CalendarDay[] = [];
    for (let i = 0; i < 42; i++) {
      const d = new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i);
      const iso = toIso(d);
      days.push({
        date: d,
        iso,
        inCurrentMonth: d.getMonth() === month,
        isPast: iso < todayIso,
        isToday: iso === todayIso,
        count: counts.get(iso) ?? 0,
      });
    }
    return days;
  }

  get countsByDate(): Map<string, number> {
    const map = new Map<string, number>();
    for (const slot of this.schedule) {
      map.set(slot.date, (map.get(slot.date) ?? 0) + 1);
    }
    return map;
  }

  get selectedDateWindows(): AvailabilitySlotInfo[] {
    if (!this.selectedDate) return [];
    return this.schedule
      .filter(s => s.date === this.selectedDate)
      .sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  ngOnInit() {
    this.doctorsService.getMine().subscribe({
      next: (doctor) => {
        this.doctorId = doctor.id;
        this.doctor = doctor;
        this.loadSchedule();
        this.loadServices();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  loadServices() {
    if (!this.doctorId) return;

    this.isLoadingServices = true;
    this.doctorsService.getServices(this.doctorId).subscribe({
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

  loadSchedule() {
    if (!this.doctorId) return;

    this.isLoading = true;
    this.appointmentsService.getSchedule(this.doctorId).subscribe({
      next: (schedule) => {
        this.schedule = schedule;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  prevMonth() {
    this.currentMonth = new Date(this.currentMonth.getFullYear(), this.currentMonth.getMonth() - 1, 1);
  }

  nextMonth() {
    this.currentMonth = new Date(this.currentMonth.getFullYear(), this.currentMonth.getMonth() + 1, 1);
  }

  goToToday() {
    const now = new Date();
    this.currentMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    this.selectedDate = toIso(now);
  }

  selectDay(day: CalendarDay) {
    if (day.isPast) return;
    this.selectedDate = day.iso;
  }

  toggleNewSlotVisitType(type: VisitType) {
    if (this.newSlotVisitTypes.includes(type)) {
      // Keep at least one type selected — a window with none would be unbookable.
      if (this.newSlotVisitTypes.length === 1) return;
      this.newSlotVisitTypes = this.newSlotVisitTypes.filter(t => t !== type);
    } else {
      this.newSlotVisitTypes = [...this.newSlotVisitTypes, type];
    }
  }

  addScheduleSlot() {
    if (!this.doctorId || !this.selectedDate) return;

    this.isAddingSlot = true;
    this.appointmentsService.addScheduleSlot(this.doctorId, {
      date: this.selectedDate,
      startTime: `${this.newSlotStart}:00`,
      endTime: `${this.newSlotEnd}:00`,
      allowedVisitTypes: this.newSlotVisitTypes,
    }).subscribe({
      next: (slot) => {
        this.schedule = [...this.schedule, slot];
        this.isAddingSlot = false;
        this.newSlotVisitTypes = ['Stationary', 'Online'];
        this.toastService.success(this.translateService.instant('DOCTOR_SCHEDULE.TOAST.ADDED'));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        this.isAddingSlot = false;

        if (err.status === 409) {
          this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.ERROR_OVERLAP'));
        } else if (err.status === 400) {
          this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.ERROR_INVALID'));
        } else {
          this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.ERROR_GENERIC'));
        }
        this.cdr.markForCheck();
      }
    });
  }

  deleteScheduleSlot(slot: AvailabilitySlotInfo) {
    if (!this.doctorId) return;

    this.deletingSlotId = slot.id;
    this.appointmentsService.deleteScheduleSlot(this.doctorId, slot.id).subscribe({
      next: () => {
        this.schedule = this.schedule.filter(s => s.id !== slot.id);
        this.deletingSlotId = null;
        this.toastService.success(this.translateService.instant('DOCTOR_SCHEDULE.TOAST.DELETED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.deletingSlotId = null;
        this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.ERROR_GENERIC'));
        this.cdr.markForCheck();
      }
    });
  }

  startAddTreatment() {
    if (this.missingVisitTypes.length === 0) return;
    this.isAddingTreatment = true;
    this.newTreatmentType = this.missingVisitTypes[0];
    this.newTreatmentPrice = null;
  }

  selectNewTreatmentType(type: VisitType) {
    this.newTreatmentType = type;
  }

  cancelAddTreatment() {
    this.isAddingTreatment = false;
    this.newTreatmentType = null;
    this.newTreatmentPrice = null;
  }

  saveNewTreatment() {
    if (!this.doctor || !this.newTreatmentType || this.newTreatmentPrice == null) return;

    const priceCents = Math.round(this.newTreatmentPrice * 100);
    const dto = {
      fullName: this.doctor.fullName,
      specialization: this.doctor.specialization,
      city: this.doctor.city,
      description: this.doctor.description,
      isActive: this.doctor.isActive,
      priceStationaryCents: this.newTreatmentType === 'Stationary' ? priceCents : this.doctor.priceStationaryCents,
      priceOnlineCents: this.newTreatmentType === 'Online' ? priceCents : this.doctor.priceOnlineCents,
      conditionsTreated: this.doctor.conditionsTreated ?? [],
    };

    this.isSavingTreatment = true;
    this.doctorsService.update(this.doctor.id, dto).subscribe({
      next: (updated) => {
        this.doctor = updated;
        this.isSavingTreatment = false;
        this.cancelAddTreatment();
        this.toastService.success(this.translateService.instant('DOCTOR_SCHEDULE.TREATMENTS.TOAST.ADDED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSavingTreatment = false;
        this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.TREATMENTS.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  removeTreatment(treatment: Treatment) {
    if (!this.doctor) return;

    const dto = {
      fullName: this.doctor.fullName,
      specialization: this.doctor.specialization,
      city: this.doctor.city,
      description: this.doctor.description,
      isActive: this.doctor.isActive,
      priceStationaryCents: treatment.type === 'Stationary' ? null : this.doctor.priceStationaryCents,
      priceOnlineCents: treatment.type === 'Online' ? null : this.doctor.priceOnlineCents,
      conditionsTreated: this.doctor.conditionsTreated ?? [],
    };

    this.removingType = treatment.type;
    this.doctorsService.update(this.doctor.id, dto).subscribe({
      next: (updated) => {
        this.doctor = updated;
        this.removingType = null;
        this.toastService.success(this.translateService.instant('DOCTOR_SCHEDULE.TREATMENTS.TOAST.REMOVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.removingType = null;
        this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.TREATMENTS.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  toggleNewServiceVisitType(type: VisitType) {
    if (this.newServiceVisitTypes.includes(type)) {
      // Keep at least one type selected — a service with none would be unbookable.
      if (this.newServiceVisitTypes.length === 1) return;
      this.newServiceVisitTypes = this.newServiceVisitTypes.filter(t => t !== type);
    } else {
      this.newServiceVisitTypes = [...this.newServiceVisitTypes, type];
    }
  }

  startAddService() {
    this.isAddingService = true;
    this.editingServiceId = null;
    this.newServiceName = '';
    this.newServiceDescription = '';
    this.newServicePrice = null;
    this.newServiceVisitTypes = ['Stationary', 'Online'];
  }

  startEditService(service: MedicalService) {
    this.isAddingService = true;
    this.editingServiceId = service.id;
    this.newServiceName = service.name;
    this.newServiceDescription = service.description ?? '';
    this.newServicePrice = service.priceCents / 100;
    this.newServiceVisitTypes = [...service.allowedVisitTypes];
  }

  cancelAddService() {
    this.isAddingService = false;
    this.editingServiceId = null;
  }

  saveService() {
    if (!this.doctorId || !this.newServiceName.trim() || this.newServiceVisitTypes.length === 0 || this.newServicePrice == null) return;

    const dto = {
      name: this.newServiceName.trim(),
      description: this.newServiceDescription.trim() || null,
      priceCents: Math.round(this.newServicePrice * 100),
      allowedVisitTypes: this.newServiceVisitTypes,
    };

    const isEditing = !!this.editingServiceId;
    this.isSavingService = true;

    const request = isEditing
      ? this.doctorsService.updateService(this.doctorId, this.editingServiceId!, dto)
      : this.doctorsService.addService(this.doctorId, dto);

    request.subscribe({
      next: (service) => {
        this.services = isEditing
          ? this.services.map(s => s.id === service.id ? service : s)
          : [...this.services, service];
        this.isSavingService = false;
        this.isAddingService = false;
        this.editingServiceId = null;
        this.toastService.success(this.translateService.instant(
          isEditing ? 'DOCTOR_SCHEDULE.SERVICES.TOAST.UPDATED' : 'DOCTOR_SCHEDULE.SERVICES.TOAST.ADDED'
        ));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSavingService = false;
        this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.SERVICES.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  deleteService(service: MedicalService) {
    if (!this.doctorId) return;

    this.deletingServiceId = service.id;
    this.doctorsService.deleteService(this.doctorId, service.id).subscribe({
      next: () => {
        this.services = this.services.filter(s => s.id !== service.id);
        this.deletingServiceId = null;
        this.toastService.success(this.translateService.instant('DOCTOR_SCHEDULE.SERVICES.TOAST.REMOVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.deletingServiceId = null;
        this.toastService.error(this.translateService.instant('DOCTOR_SCHEDULE.SERVICES.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }
}
