import { Component, inject, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { DoctorsService } from '../services/doctors/doctors';
import { AuthService } from '../services/auth/auth';
import { LanguageSwitcherComponent } from '../shared/language-switcher/language-switcher.component';
import { initials } from '../shared/initials';

/** Persistent left-hand nav for the doctor-only pages (dashboard, schedule, own
 *  profile) — doctors don't see the site-wide navbar, so this is their only nav
 *  and carries the language switcher too. */
@Component({
  selector: 'app-doctor-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, TranslatePipe, LanguageSwitcherComponent],
  templateUrl: './doctor-sidebar.html',
})
export class DoctorSidebarComponent implements OnInit {
  doctorsService = inject(DoctorsService);
  authService = inject(AuthService);

  readonly initials = initials;

  ngOnInit() {
    // The sidebar (not the page it's embedded in) is what needs ownProfile() —
    // pages like doctor-schedule fetch their own doctor data separately and never
    // used to populate this signal now that the navbar (which always did) is
    // hidden for doctors. Safe to call repeatedly; it just refreshes the signal.
    if (!this.doctorsService.ownProfile()) {
      this.doctorsService.checkOwnProfile().subscribe({ error: () => {} });
    }
  }
}
