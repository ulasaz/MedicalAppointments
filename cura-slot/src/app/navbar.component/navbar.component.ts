import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../services/auth/auth';
import { DoctorsService } from '../services/doctors/doctors';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageSwitcherComponent } from '../shared/language-switcher/language-switcher.component';

@Component({
  selector: 'app-navbar',
  imports: [CommonModule, RouterLink, RouterLinkActive, TranslatePipe, LanguageSwitcherComponent],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css',
})
export class NavbarComponent implements OnInit {
  authService = inject(AuthService);
  doctorsService = inject(DoctorsService);

  ngOnInit() {
    if (this.authService.getUserRole() === 'Doctor') {
      this.doctorsService.checkOwnProfile().subscribe({ error: () => {} });
    }
  }

  getAppointmentsLink(): string[] {
    const role = this.authService.getUserRole();
    if (role === 'Doctor') return ['/doctor-dashboard'];
    if (role === 'Admin') return ['/admin'];
    return ['/appointments'];
  }

  getProfileLink(): string[] {
    const role = this.authService.getUserRole();
    if (role === 'Doctor') {
      const profile = this.doctorsService.ownProfile();
      return profile ? ['/doctors', profile.id] : ['/doctor-setup'];
    }
    if (role === 'Admin') return ['/admin'];
    return ['/patient-profile'];
  }
}
