import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Avatar + "upload photo" control shared by the doctor and patient profile pages.
 *  The component itself is display-only for the network round-trip — it emits the
 *  selected File (and previews it locally) and lets the parent decide whether to
 *  actually persist it. Doctor profiles persist via DoctorsService; patient profiles
 *  currently don't have a photo-storage endpoint, so patient-profile just ignores
 *  the output and keeps the old local-preview-only behavior. */
@Component({
  selector: 'app-profile-avatar',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './profile-avatar.html',
})
export class ProfileAvatarComponent {
  @Input({ required: true }) initials!: string;
  /** Server-stored photo URL, shown until/unless a new local preview overrides it. */
  @Input() photoUrl: string | null = null;
  /** Whether a stored photo exists to offer removing. */
  @Input() canRemove = false;
  /** Whether the parent will actually persist a selected file (controls the hint text). */
  @Input() persisted = false;

  @Output() fileSelected = new EventEmitter<File>();
  @Output() photoRemoved = new EventEmitter<void>();

  previewUrl: string | null = null;

  onFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      this.previewUrl = reader.result as string;
    };
    reader.readAsDataURL(file);

    this.fileSelected.emit(file);
  }

  removePhoto() {
    this.previewUrl = null;
    this.photoRemoved.emit();
  }
}
