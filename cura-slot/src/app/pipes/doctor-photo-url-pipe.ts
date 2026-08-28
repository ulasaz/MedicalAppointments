import { Pipe, PipeTransform, inject } from '@angular/core';
import { DoctorsService } from '../services/doctors/doctors';

@Pipe({
  name: 'doctorPhotoUrl',
  standalone: true
})
export class DoctorPhotoUrlPipe implements PipeTransform {
  private doctorsService = inject(DoctorsService);

  transform(doctorId: string): string {
    return this.doctorsService.photoUrl(doctorId);
  }
}
