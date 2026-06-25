import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CafeService {

  private httpClient = inject(HttpClient);
  private baseUrl = 'http://localhost:5000/api/orders';

  getAllOrders(status?: string) {
    let url = this.baseUrl;
    if (status) {
      url += `?status=${status}`;
    }
    return this.httpClient.get(url);
  }

  private menuUrl = 'http://localhost:5000/api/menu/items';

  parseGoogleDrive(folderId: string) {
    const url = `${this.menuUrl}/parser/GoogleDrive`;
    return this.httpClient.get(url, { params: { folderId } });
  }

  parseFacebook(url: string) {
    return this.httpClient.get(`${this.menuUrl}/parser/Facebook`, { params: { url } });
  }

  saveParsedMenu(items: any[]) {
    const url = `${this.menuUrl}/parser/save`;
    return this.httpClient.post(url, items);
  }

  acceptOrder(id: string) {
    const url = `${this.baseUrl}/${id}/accept`;
    return this.httpClient.post(url, null);
  }

  rejectOrder(id: string, reason: string) {
    const url = `${this.baseUrl}/${id}/reject`;
    const params = new HttpParams().set('reason', reason);
    return this.httpClient.post(url, null, { params });
  }
    confirmOrder(id: string,) {
    const url = `${this.baseUrl}/${id}/confirm`;
    return this.httpClient.post(url, null);
  }
    readyOrder(id: string,) {
    const url = `${this.baseUrl}/${id}/ready`;
    return this.httpClient.post(url, null);
  }
}
