import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CafeService } from '../services/cafe/cafe';
import { AuthService } from '../services/auth/auth';
import { ToastService } from '../services/toast/toast';
import { MenuService } from '../services/menu/menu';
import { ScheduleService } from '../services/schedule/schedule';
import { PricePipe } from '../pipes/price-pipe';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-cafe-panel',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, PricePipe, TranslatePipe],
  templateUrl: './cafe-panel.html',
  styleUrl: './cafe-panel.css',
})
export class CafePanel implements OnInit {

  readonly statuses = ['Pending', 'Confirmed', 'Rejected', 'Cancelled', 'Completed', 'Ready'];
  readonly days = [0, 1, 2, 3, 4, 5, 6];

  private cafeService    = inject(CafeService);
  private scheduleService = inject(ScheduleService);
  private menuService    = inject(MenuService);
  private cdr            = inject(ChangeDetectorRef);
  private authService    = inject(AuthService);
  private toastService   = inject(ToastService);
  private translateService = inject(TranslateService);

  ordersByStatus: Record<string, any[]> = {
    Pending: [], Confirmed: [], Cancelled: [], Rejected: [], Completed: [], Ready: [],
  };
  orders: any[] = [];
  isParsing = false;
  isFacebookParsing = false;
  parsedItems: any[] = [];
  folderId = '1pl-odFhV41SECV9ejF8-uiTlOZyYu4Nf';
  facebookUrl = '';
  reasonForm = new FormGroup({
    reason: new FormControl('', [Validators.required, Validators.minLength(3)]),
  });
  isRejectModalOpen = false;
  selectedOrderIdToReject: string | null = null;

  activeTab: 'orders' | 'schedule' | 'parsing' = 'orders';
  scheduleByDay: Record<number, any[]> = { 0:[], 1:[], 2:[], 3:[], 4:[], 5:[], 6:[] };
  menuItems: any[] = [];
  selectedItemIds: Record<number, string> = { 0:'', 1:'', 2:'', 3:'', 4:'', 5:'', 6:'' };
  private scheduleLoaded = false;

   ngOnInit() {
    this.loadAllOrders();
  }

  setActiveTab(tab: 'orders' | 'schedule' | 'parsing') {
    this.activeTab = tab;
    if (tab === 'schedule' && !this.scheduleLoaded) {
      this.loadSchedule();
      this.loadMenuItems();
    }
  }

  startParsing() {
    if (!this.authService.isAuthenticated() || !this.folderId) return;

    this.isParsing = true;
    this.parsedItems = [];

    this.cafeService.parseGoogleDrive(this.folderId).subscribe({
      next: (response: any) => {
        const data = response.data || response;
        this.parsedItems = Array.isArray(data) ? data : data?.$values ?? [];
        this.isParsing = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Error during parsing', err);
        this.isParsing = false;
        this.toastService.error(this.translateService.instant('CAFE_PANEL.PARSING.TOAST.ERROR'));
        this.cdr.markForCheck();
      },
    });
  }
  startFacebookParsing() {
    if (!this.authService.isAuthenticated() || !this.facebookUrl) return;

    this.isFacebookParsing = true;
    this.parsedItems = [];

    this.cafeService.parseFacebook(this.facebookUrl).subscribe({
      next: (response: any) => {
        const data = response.data || response;
        this.parsedItems = Array.isArray(data) ? data : data?.$values ?? [];
        this.isFacebookParsing = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Error during Facebook parsing', err);
        this.isFacebookParsing = false;
        this.toastService.error(this.translateService.instant('CAFE_PANEL.PARSING.TOAST.ERROR'));
        this.cdr.markForCheck();
      },
    });
  }

  removeParsedItem(index: number) {
    this.parsedItems.splice(index, 1);
  }

  saveParsedMenu() {
    if (this.parsedItems.length === 0) return;

    const dtos = this.parsedItems.map(item => ({
      categoryName: item.category?.name ?? '',
      name: item.name,
      description: item.description,
      priceMinor: item.priceMinor,
      isAvailable: item.isAvailable ?? true,
      photoUrl: item.photoUrl ?? null,
    }));

    this.cafeService.saveParsedMenu(dtos).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('CAFE_PANEL.PARSING.TOAST.SAVED'));
        this.parsedItems = [];
        this.loadMenuItems();
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Error saving menu', err);
        this.toastService.error(this.translateService.instant('CAFE_PANEL.PARSING.TOAST.SAVE_ERROR'));
      }
    });
  }
  loadAllOrders() {
    if (!this.authService.isAuthenticated()) return;
    this.cafeService.getAllOrders().subscribe({
      next: (response: any) => {
        this.orders = response;
        this.distributeOrders();
        this.cdr.markForCheck();
      },
      error: (err) => console.error('Error loading orders', err),
    });
  }


  private distributeOrders() {
    this.statuses.forEach(s => (this.ordersByStatus[s] = []));
    this.orders.forEach(order => {
      if (this.ordersByStatus[order.status]) {
        this.ordersByStatus[order.status].push(order);
      }
    });
  }

  openRejectModal(orderId: string) {
    this.selectedOrderIdToReject = orderId;
    this.reasonForm.reset();
    this.isRejectModalOpen = true;
  }

  closeRejectModal() {
    this.isRejectModalOpen = false;
    this.selectedOrderIdToReject = null;
  }

  confirmReject() {
    if (this.reasonForm.invalid || !this.selectedOrderIdToReject) return;
    const reason = this.reasonForm.value.reason ?? '';
    this.cafeService.rejectOrder(this.selectedOrderIdToReject, reason).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('CAFE_PANEL.TOAST.REJECTED', { id: this.selectedOrderIdToReject }));
        this.closeRejectModal();
        this.loadAllOrders();
      },
      error: (err) => console.error('Error rejecting order', err),
    });
  }

  confirmOrder(id: string) {
    this.cafeService.confirmOrder(id).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('CAFE_PANEL.TOAST.CONFIRMED', { id }));
        this.loadAllOrders();
      },
      error: (err) => console.error('Error confirming order', err),
    });
  }

  readyOrder(id: string) {
    this.cafeService.readyOrder(id).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('CAFE_PANEL.TOAST.READY', { id }));
        this.loadAllOrders();
      },
      error: (err) => console.error('Error marking order ready', err),
    });
  }

  private loadMenuItems() {
    this.menuService.getAllMenuItemsUnfiltered().subscribe({
      next: (items: any) => {
        this.menuItems = Array.isArray(items) ? items : (items as any)?.$values ?? [];
        this.cdr.markForCheck();
      },
      error: (err) => console.error('Error loading menu items', err),
    });
  }

  loadSchedule() {
    this.days.forEach(day => this.reloadDay(day));
    this.scheduleLoaded = true;
  }

  addToSchedule(day: number) {
    const menuItemId = this.selectedItemIds[day];
    if (!menuItemId) return;
    this.scheduleService.addToSchedule(day, menuItemId).subscribe({
      next: () => {
        this.selectedItemIds[day] = '';
        this.reloadDay(day);
        this.toastService.info(this.translateService.instant('CAFE_PANEL.SCHEDULE.TOAST.ADDED'));
      },
      error: (err) => {
        console.error('Error adding to schedule', err);
        this.toastService.info(this.translateService.instant('CAFE_PANEL.SCHEDULE.TOAST.ADD_ERROR'));
      },
    });
  }

  removeFromSchedule(scheduleId: string, day: number) {
    this.scheduleService.removeFromSchedule(scheduleId).subscribe({
      next: () => {
        this.reloadDay(day);
        this.toastService.info(this.translateService.instant('CAFE_PANEL.SCHEDULE.TOAST.REMOVED'));
      },
      error: (err) => {
        console.error('Error removing from schedule', err);
        this.toastService.info(this.translateService.instant('CAFE_PANEL.SCHEDULE.TOAST.REMOVE_ERROR'));
      },
    });
  }

  private reloadDay(day: number) {
    this.scheduleService.getScheduleForDay(day).subscribe({
      next: (items) => {
        this.scheduleByDay[day] = items;
        this.cdr.markForCheck();
      },
      error: (err) => console.error(`Error loading schedule for day ${day}`, err),
    });
  }
}
