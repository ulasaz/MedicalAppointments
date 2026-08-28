import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { TenantService } from '../../services/tenant/tenant';

/**
 * Shown on the login/register forms so it's never ambiguous which medical center you're
 * about to authenticate against — with a one-click way to switch before submitting.
 * Purely a TenantService front-end; doesn't touch auth state itself.
 */
@Component({
  selector: 'app-center-badge',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './center-badge.component.html',
})
export class CenterBadgeComponent {
  tenantService = inject(TenantService);
  isOpen = signal(false);

  toggle() {
    this.isOpen.set(!this.isOpen());
  }

  select(id: string) {
    this.tenantService.selectTenant(id);
    this.isOpen.set(false);
  }
}
