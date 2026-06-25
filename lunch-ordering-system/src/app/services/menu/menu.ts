import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MenuService {
  private httpClient = inject(HttpClient);
  private baseUrl = 'http://localhost:5000/api/menu/items';

  getAllMenuItems() {
    return this.httpClient.get(this.baseUrl);
  }

  getAllMenuItemsUnfiltered() {
    return this.httpClient.get(`${this.baseUrl}/all`);
  }

  getItemById(id: string) {
    return this.httpClient.get(`${this.baseUrl}/${id}`);
  }

  addItem(menuItemData: any) {
    return this.httpClient.post(this.baseUrl, menuItemData);
  }

  updateItem(id: string, menuItemData: any) {
    return this.httpClient.put(`${this.baseUrl}/${id}`, menuItemData);
  }

  deleteItem(id: string) {
    return this.httpClient.delete(`${this.baseUrl}/${id}`);
  }

  toggleAvailability(id: string, isAvailable: boolean) {
    return this.httpClient.patch(`${this.baseUrl}/${id}/availability`, { isAvailable });
  }
}
