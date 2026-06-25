import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, OnInit, OnDestroy } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../services/auth/auth';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { TenantConfig, TenantService } from '../services/tenant.service';

@Component({
  selector: 'app-navbar',
  imports: [CommonModule, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css',
})
export class NavbarComponent implements OnInit, OnDestroy {
  authService = inject(AuthService);
  translate = inject(TranslateService);
  tenantService = inject(TenantService);

  isScrolled = false;
  currentLang = 'en';
  tenantConfig: TenantConfig | null = null;

  private langSub!: Subscription;
  private tenantSub!: Subscription;

  ngOnInit() {
    this.currentLang = localStorage.getItem('language') ?? 'en';
    this.langSub = this.translate.onLangChange.subscribe(({ lang }) => {
      this.currentLang = lang;
    });

    this.tenantSub = this.tenantService.config$.subscribe(config =>{
      this.tenantConfig = config;
    })
  }

  ngOnDestroy() {
    this.langSub?.unsubscribe();
    this.tenantSub?.unsubscribe();
  }

  switchLang(lang: string) {
    this.translate.use(lang);
    localStorage.setItem('language', lang);
  }


  @HostListener('window:scroll', [])
  onWindowScroll() {
    this.isScrolled = window.scrollY > 20;
  }
}
