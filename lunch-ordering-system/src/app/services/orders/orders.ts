import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OrdersService {
  private httpClient = inject(HttpClient)

  placeNewOrder(userData: any){

      const gatewayUrl = 'http://localhost:5000/api/orders'; 

      return this.httpClient.post(gatewayUrl, userData)
  }

  getMyOrders(){

      const gatewayUrl = 'http://localhost:5000/api/orders/mine'; 

      return this.httpClient.get(gatewayUrl)
  }

  completeOrder(id: string,) {
    const url = `http://localhost:5000/api/orders/${id}/complete`;
    return this.httpClient.post(url, null);
  }
  
  cancelOrder(id: string,) {
    const url = `http://localhost:5000/api/orders/${id}/cancel`;
    return this.httpClient.post(url, null);
  }
}
