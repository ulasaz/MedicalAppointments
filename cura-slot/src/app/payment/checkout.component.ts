import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { loadStripe, Stripe, StripeElements } from '@stripe/stripe-js';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CheckoutService } from '../services/checkout/checkout';
import { AppointmentInfo, AppointmentsService } from '../services/appointments/appointments';
import { ToastService } from '../services/toast/toast';
import { PricePipe } from '../pipes/price-pipe';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe, PricePipe],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.css',
})
export class CheckoutComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private checkoutService = inject(CheckoutService);
  private appointmentsService = inject(AppointmentsService);
  private toastService = inject(ToastService);
  private translateService = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  appointment: AppointmentInfo | null = null;
  isLoading = true;
  notPayable = false;
  isPaying = false;

  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;
  appointmentId = '';

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notPayable = true;
      this.isLoading = false;
      return;
    }
    this.appointmentId = id;

    try {
      this.appointment = await firstValueFrom(this.appointmentsService.getById(id));
    } catch {
      this.notPayable = true;
      this.isLoading = false;
      this.cdr.markForCheck();
      return;
    }

    if (this.appointment.visitType !== 'Online' || this.appointment.isPaid) {
      this.notPayable = true;
      this.isLoading = false;
      this.cdr.markForCheck();
      return;
    }

    try {
      const stripe = await loadStripe(environment.stripePublishableKey);
      if (!stripe) throw new Error('Stripe failed to load');
      this.stripe = stripe;

      const { clientSecret } = await firstValueFrom(
        this.checkoutService.createPaymentIntent({ appointmentId: id })
      );

      this.elements = stripe.elements({ clientSecret });
      this.isLoading = false;
      this.cdr.detectChanges();
      this.elements.create('payment').mount('#payment-element');
    }
    catch (error) {
    console.error('PAYMENT ERROR:', error);
    this.notPayable = true;
    this.toastService.error(this.translateService.instant('CHECKOUT.ERROR_INIT'));
    } finally {
      this.isLoading = false;
      this.cdr.markForCheck();
    }
  }

  async pay() {
    if (!this.stripe || !this.elements) return;

    this.isPaying = true;
    this.cdr.markForCheck();

    const { error } = await this.stripe.confirmPayment({
      elements: this.elements,
      confirmParams: {
        return_url: `${window.location.origin}/appointments/${this.appointmentId}/payment-success`,
      },
    });

    if (error) {
      this.isPaying = false;
      this.toastService.error(error.message || this.translateService.instant('CHECKOUT.ERROR_PAYMENT'));
      this.cdr.markForCheck();
    }
  }
}
