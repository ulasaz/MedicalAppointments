import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CategoriesService {
    private httpClient = inject(HttpClient)

  getAllCategories(){
    const gatewayUrl = 'http://localhost:5000/api/menu/categories'; 

    return this.httpClient.get(gatewayUrl)
  }
}
