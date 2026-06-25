import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrdersService } from '../services/orders/orders';
import { PricePipe } from '../pipes/price-pipe';
import { ToastService } from '../services/toast/toast';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-orders',
  imports: [RouterLink, PricePipe, TranslatePipe],
  templateUrl: './orders.html',
  styleUrl: './orders.css',
})
export class Orders implements OnInit {

  private ordersService = inject(OrdersService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  orders: any[] = [];

  ngOnInit() {
    this.loadMyOrders();
  }

  loadMyOrders() {
    this.ordersService.getMyOrders().subscribe({
      next: (response: any) => {
        this.orders = response;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Error during getting orders:', err);
      }
    });
  }

  public completeOrder(id: string) {
    this.ordersService.completeOrder(id).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('ORDERS.TOAST.COMPLETED', { id }));
        this.loadMyOrders();
      },
      error: (err) => {
        console.error('Error during completing order', err);
        this.toastService.error(this.translateService.instant('ORDERS.TOAST.COMPLETE_ERROR'));
      }
    });
  }

  public cancelOrder(id: string) {
    this.ordersService.cancelOrder(id).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('ORDERS.TOAST.CANCELLED', { id }));
        this.loadMyOrders();
      },
      error: (err) => {
        console.error('Error during cancelling order', err);
        this.toastService.error(this.translateService.instant('ORDERS.TOAST.CANCEL_ERROR'));
      }
    });
  }
}
