import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface CreateIntentDto {
  appointmentId: string;
}

export interface CreateIntentResponse {
  clientSecret: string;
}

export interface ConfirmPaymentDto {
  paymentIntentId: string;
}

export interface ConfirmPaymentResponse {
  isPaid: boolean;
}

@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private httpClient = inject(HttpClient);
  private baseUrl = environment.gatewayApiUrl;

  createPaymentIntent(dto: CreateIntentDto) {
    return this.httpClient.post<CreateIntentResponse>(
      `${this.baseUrl}/payments/create-intent`, dto);
  }

  confirmPayment(dto: ConfirmPaymentDto) {
    return this.httpClient.post<ConfirmPaymentResponse>(
      `${this.baseUrl}/payments/confirm`, dto);
  }
}
