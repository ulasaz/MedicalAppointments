import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

export interface DoctorProfileFormValue {
  fullName: string;
  specialization: string;
  city: string;
  description?: string | null;
  isActive: boolean;
  priceStationaryCents?: number | null;
  priceOnlineCents?: number | null;
}

function centsToMajorUnits(cents?: number | null): number | null {
  return cents == null ? null : cents / 100;
}

function majorUnitsToCents(value: number | string | null): number | null {
  if (value === null || value === '') return null;
  const parsed = typeof value === 'number' ? value : parseFloat(value);
  return Number.isFinite(parsed) ? Math.round(parsed * 100) : null;
}

@Component({
  selector: 'app-doctor-profile-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './doctor-profile-form.html',
})
export class DoctorProfileFormComponent implements OnChanges {
  @Input() initial: DoctorProfileFormValue | null = null;
  @Input() isSaving = false;
  @Input() showCancel = true;
  @Input() submitLabelKey = 'DOCTOR_PROFILE.FORM.SAVE';
  @Output() formSubmit = new EventEmitter<DoctorProfileFormValue>();
  @Output() cancel = new EventEmitter<void>();

  form = new FormGroup({
    fullName: new FormControl('', [Validators.required]),
    specialization: new FormControl('', [Validators.required]),
    city: new FormControl('', [Validators.required]),
    description: new FormControl(''),
    isActive: new FormControl(true),
    priceStationary: new FormControl<number | null>(null, [Validators.min(0)]),
    priceOnline: new FormControl<number | null>(null, [Validators.min(0)]),
  });

  ngOnChanges(changes: SimpleChanges) {
    if (changes['initial'] && this.initial) {
      this.form.setValue({
        fullName: this.initial.fullName,
        specialization: this.initial.specialization,
        city: this.initial.city,
        description: this.initial.description ?? '',
        isActive: this.initial.isActive,
        priceStationary: centsToMajorUnits(this.initial.priceStationaryCents),
        priceOnline: centsToMajorUnits(this.initial.priceOnlineCents),
      });
    }
  }

  submit() {
    if (this.form.invalid) return;

    const value = this.form.getRawValue();

    this.formSubmit.emit({
      fullName: value.fullName ?? '',
      specialization: value.specialization ?? '',
      city: value.city ?? '',
      description: value.description || null,
      isActive: value.isActive ?? true,
      priceStationaryCents: majorUnitsToCents(value.priceStationary),
      priceOnlineCents: majorUnitsToCents(value.priceOnline),
    });
  }
}
