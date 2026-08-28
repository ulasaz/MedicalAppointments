import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, UserProfile } from '../services/auth/auth';
import { DoctorProfile, DoctorsService } from '../services/doctors/doctors';
import { AdminStats, AppointmentsService, DoctorReview } from '../services/appointments/appointments';
import { ToastService } from '../services/toast/toast';
import { DoctorPhotoUrlPipe } from '../pipes/doctor-photo-url-pipe';
import { initials } from '../shared/initials';
import { AVAILABLE_FONTS, AVAILABLE_RADII, CreateMedicalCenterRequest, TenantService, UpdateMedicalCenterRequest } from '../services/tenant/tenant';

type AdminTab = 'users' | 'doctors' | 'reviews' | 'centers' | 'branding';
type RoleFilter = 'All' | 'Patient' | 'Doctor' | 'Admin';
type StatusFilter = 'All' | 'Active' | 'Inactive';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TranslatePipe, DoctorPhotoUrlPipe],
  templateUrl: './admin.html',
  styleUrl: './admin.css',
})
export class AdminPage implements OnInit {
  private authService = inject(AuthService);
  private doctorsService = inject(DoctorsService);
  private appointmentsService = inject(AppointmentsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  tenantService = inject(TenantService);

  readonly initials = initials;
  readonly roleOptions: RoleFilter[] = ['All', 'Patient', 'Doctor', 'Admin'];
  readonly statusOptions: StatusFilter[] = ['All', 'Active', 'Inactive'];

  activeTab: AdminTab = 'users';
  private loadedTabs = new Set<AdminTab>();

  stats: AdminStats | null = null;
  isLoadingStats = true;

  users: UserProfile[] = [];
  isLoadingUsers = true;
  processingUserId: string | null = null;
  userQuery = '';
  roleFilter: RoleFilter = 'All';
  userStatusFilter: StatusFilter = 'All';

  doctors: DoctorProfile[] = [];
  isLoadingDoctors = false;
  processingDoctorId: string | null = null;
  doctorQuery = '';
  doctorStatusFilter: StatusFilter = 'All';

  reviews: DoctorReview[] = [];
  isLoadingReviews = false;
  processingReviewId: string | null = null;

  isLoadingCenters = false;
  isCreatingCenter = false;
  showCreateCenterForm = false;
  newCenter: CreateMedicalCenterRequest = { name: '', slug: '', primaryColorHex: '#f43f5e', adminEmail: '', adminPassword: '', adminDisplayName: '' };

  isSavingBranding = false;
  brandingForm: UpdateMedicalCenterRequest = { name: '', primaryColorHex: '#f43f5e', fontFamily: 'Inter', buttonRadius: 'pill', bannerVideoUrl: '' };
  linkCopied = false;
  isUploadingBanner = false;
  readonly availableFonts = AVAILABLE_FONTS;
  readonly availableRadii = AVAILABLE_RADII;

  get ownUserId(): string | null {
    return this.authService.getUserId();
  }

  /** The shareable, anonymous-browsing entry link for a center (see the `/c/:slug` route). */
  publicLink(slug: string): string {
    return `${window.location.origin}/c/${slug}`;
  }

  copyPublicLink(slug: string) {
    navigator.clipboard.writeText(this.publicLink(slug)).then(() => {
      this.linkCopied = true;
      setTimeout(() => { this.linkCopied = false; this.cdr.markForCheck(); }, 2000);
      this.cdr.markForCheck();
    });
  }

  /** The platform super-admin has no tenant_id claim; every other admin is scoped to one center. */
  get isSuperAdmin(): boolean {
    return this.authService.getTenantId() === null;
  }

  /** The super-admin's user list comes back unscoped (they have no tenant of their own to
   * filter by server-side) — so once they're "viewing" a specific center via the switcher,
   * narrow it client-side to match Doctors/Reviews/Stats, which already are. */
  get usersInViewedCenter(): UserProfile[] {
    const viewingCenterId = this.isSuperAdmin ? this.tenantService.selectedTenantId() : null;
    if (!viewingCenterId) return this.users;
    return this.users.filter((user) => user.tenantId === viewingCenterId);
  }

  get filteredUsers(): UserProfile[] {
    const query = this.userQuery.trim().toLowerCase();
    return this.usersInViewedCenter.filter((user) => {
      const matchesQuery = !query
        || user.displayName.toLowerCase().includes(query)
        || user.email.toLowerCase().includes(query);
      const matchesRole = this.roleFilter === 'All' || user.role === this.roleFilter;
      const matchesStatus = this.userStatusFilter === 'All'
        || (this.userStatusFilter === 'Active') === user.isActive;
      return matchesQuery && matchesRole && matchesStatus;
    });
  }

  get filteredDoctors(): DoctorProfile[] {
    const query = this.doctorQuery.trim().toLowerCase();
    return this.doctors.filter((doctor) => {
      const matchesQuery = !query
        || doctor.fullName.toLowerCase().includes(query)
        || doctor.specialization.toLowerCase().includes(query)
        || doctor.city.toLowerCase().includes(query);
      const matchesStatus = this.doctorStatusFilter === 'All'
        || (this.doctorStatusFilter === 'Active') === doctor.isActive;
      return matchesQuery && matchesStatus;
    });
  }

  ngOnInit() {
    this.loadStats();
    this.loadUsers();
    this.loadedTabs.add('users');
    this.syncBrandingFormFromViewedCenter();
  }

  private syncBrandingFormFromViewedCenter() {
    const center = this.tenantService.selectedTenant();
    if (!center) return;

    this.brandingForm = {
      name: center.name,
      primaryColorHex: center.primaryColorHex,
      fontFamily: center.fontFamily,
      buttonRadius: center.buttonRadius,
      bannerVideoUrl: center.bannerVideoUrl ?? '',
    };
  }

  /** Super-admin only: switches which center's doctors/reviews/stats/branding are shown,
   * via the same X-Tenant-Id fallback anonymous browsing already relies on (their JWT
   * itself carries no tenant_id claim, so this header is what Doctors.API/Appointments.API
   * actually resolve tenant context from once this is called). */
  switchViewingCenter(centerId: string) {
    this.tenantService.selectTenant(centerId);
    this.loadedTabs.delete('doctors');
    this.loadedTabs.delete('reviews');
    this.loadStats();
    if (this.activeTab === 'doctors') this.loadDoctors();
    if (this.activeTab === 'reviews') this.loadReviews();
    this.syncBrandingFormFromViewedCenter();
    this.cdr.markForCheck();
  }

  viewCenterData(centerId: string) {
    this.switchViewingCenter(centerId);
    this.selectTab('doctors');
  }

  selectTab(tab: AdminTab) {
    this.activeTab = tab;
    if (this.loadedTabs.has(tab)) return;
    this.loadedTabs.add(tab);

    if (tab === 'doctors') this.loadDoctors();
    if (tab === 'reviews') this.loadReviews();
    if (tab === 'centers') this.loadCenters();
  }

  private loadCenters() {
    this.isLoadingCenters = true;
    this.tenantService.loadAll().subscribe({
      next: () => {
        this.isLoadingCenters = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingCenters = false;
        this.cdr.markForCheck();
      }
    });
  }

  createCenter() {
    if (!this.newCenter.name || !this.newCenter.slug || !this.newCenter.adminEmail || !this.newCenter.adminPassword || !this.newCenter.adminDisplayName) {
      return;
    }

    this.isCreatingCenter = true;
    this.tenantService.create(this.newCenter).subscribe({
      next: (center) => {
        this.tenantService.centers.update((all) => [...all, center]);
        this.isCreatingCenter = false;
        this.showCreateCenterForm = false;
        this.newCenter = { name: '', slug: '', primaryColorHex: '#f43f5e', adminEmail: '', adminPassword: '', adminDisplayName: '' };
        this.toastService.success(this.translateService.instant('ADMIN.CENTERS.TOAST.CREATED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isCreatingCenter = false;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  saveBranding() {
    const centerId = this.tenantService.selectedTenantId();
    if (!centerId || !this.brandingForm.name || !this.brandingForm.primaryColorHex) return;

    this.isSavingBranding = true;
    this.tenantService.update(centerId, this.brandingForm).subscribe({
      next: () => {
        this.isSavingBranding = false;
        this.toastService.success(this.translateService.instant('ADMIN.BRANDING.TOAST.SAVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSavingBranding = false;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  get previewFontStack(): string {
    return this.availableFonts.find((f) => f.name === this.brandingForm.fontFamily)?.stack ?? this.availableFonts[0].stack;
  }

  get previewRadiusPx(): string {
    return this.availableRadii.find((r) => r.value === this.brandingForm.buttonRadius)?.px ?? this.availableRadii[0].px;
  }

  onBannerSelected(event: Event) {
    const centerId = this.tenantService.selectedTenantId();
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!centerId || !file) return;

    this.isUploadingBanner = true;
    this.tenantService.uploadBanner(centerId, file).subscribe({
      next: () => {
        this.isUploadingBanner = false;
        this.toastService.success(this.translateService.instant('ADMIN.BRANDING.TOAST.BANNER_UPDATED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.isUploadingBanner = false;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  removeBanner() {
    const centerId = this.tenantService.selectedTenantId();
    if (!centerId) return;

    this.tenantService.deleteBanner(centerId).subscribe({
      next: () => {
        this.toastService.success(this.translateService.instant('ADMIN.BRANDING.TOAST.BANNER_REMOVED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  private loadStats() {
    this.isLoadingStats = true;
    this.appointmentsService.getAdminStats().subscribe({
      next: (stats) => {
        this.stats = stats;
        this.isLoadingStats = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingStats = false;
        this.cdr.markForCheck();
      }
    });
  }

  private loadUsers() {
    this.isLoadingUsers = true;
    this.authService.listUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.isLoadingUsers = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingUsers = false;
        this.cdr.markForCheck();
      }
    });
  }

  private loadDoctors() {
    this.isLoadingDoctors = true;
    this.doctorsService.getAllForAdmin().subscribe({
      next: (doctors) => {
        this.doctors = doctors;
        this.isLoadingDoctors = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingDoctors = false;
        this.cdr.markForCheck();
      }
    });
  }

  private loadReviews() {
    this.isLoadingReviews = true;
    this.appointmentsService.getAllReviewsForAdmin().subscribe({
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

  toggleUserStatus(user: UserProfile) {
    this.processingUserId = user.id;
    this.authService.setUserStatus(user.id, !user.isActive).subscribe({
      next: (updated) => {
        user.isActive = updated.isActive;
        this.processingUserId = null;
        this.toastService.success(this.translateService.instant(
          updated.isActive ? 'ADMIN.TOAST.ACTIVATED' : 'ADMIN.TOAST.DEACTIVATED'
        ));
        this.cdr.markForCheck();
      },
      error: () => {
        this.processingUserId = null;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  toggleDoctorStatus(doctor: DoctorProfile) {
    this.processingDoctorId = doctor.id;
    this.doctorsService.setActiveStatusAsAdmin(doctor.id, !doctor.isActive).subscribe({
      next: (updated) => {
        doctor.isActive = updated.isActive;
        this.processingDoctorId = null;
        this.toastService.success(this.translateService.instant(
          updated.isActive ? 'ADMIN.TOAST.DOCTOR_ACTIVATED' : 'ADMIN.TOAST.DOCTOR_DEACTIVATED'
        ));
        this.cdr.markForCheck();
      },
      error: () => {
        this.processingDoctorId = null;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }

  deleteReview(review: DoctorReview) {
    this.processingReviewId = review.id;
    this.appointmentsService.deleteReviewAsAdmin(review.id).subscribe({
      next: () => {
        this.reviews = this.reviews.filter((r) => r.id !== review.id);
        this.processingReviewId = null;
        this.toastService.success(this.translateService.instant('ADMIN.TOAST.REVIEW_DELETED'));
        this.cdr.markForCheck();
      },
      error: () => {
        this.processingReviewId = null;
        this.toastService.error(this.translateService.instant('ADMIN.TOAST.ERROR'));
        this.cdr.markForCheck();
      }
    });
  }
}
