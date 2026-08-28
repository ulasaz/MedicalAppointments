import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CheckoutService } from '../services/checkout/checkout';
import { ToastService } from '../services/toast/toast';

@Component({
  selector: 'app-payment-success',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe],
  templateUrl: './payment-success.html',
  styleUrl: './payment-success.css',
})
export class PaymentSuccessPage implements OnInit {
  private route = inject(ActivatedRoute);
  private checkoutService = inject(CheckoutService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  appointmentId = '';
  isLoading = true;
  isPaid = false;

  async ngOnInit() {
    this.appointmentId = this.route.snapshot.paramMap.get('id') ?? '';
    const paymentIntentId = this.route.snapshot.queryParamMap.get('payment_intent');
    const redirectStatus = this.route.snapshot.queryParamMap.get('redirect_status');

    if (!paymentIntentId || redirectStatus !== 'succeeded') {
      this.isLoading = false;
      return;
    }

    try {
      // Stripe already confirmed the payment client-side; this call makes the backend
      // verify the intent's status directly with Stripe and flip Appointment.IsPaid,
      // instead of relying solely on the webhook (which may not be reachable locally).
      const result = await firstValueFrom(this.checkoutService.confirmPayment({ paymentIntentId }));
      this.isPaid = result.isPaid;
    } catch {
      this.toastService.error(this.translateService.instant('PAYMENT_SUCCESS.ERROR_CONFIRM'));
    } finally {
      this.isLoading = false;
      this.cdr.markForCheck();
    }
  }
}
