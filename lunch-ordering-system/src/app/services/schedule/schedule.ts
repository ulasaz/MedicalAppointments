import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ScheduleService {
  private http = inject(HttpClient);
  private baseUrl = 'http://localhost:5000/api/menu/items/schedule';

  private toCsharpDay(day: number): number {
    return (day + 1) % 7;
  }

  getScheduleForDay(dayOfWeek: number) {
    return this.http.get<any[]>(`${this.baseUrl}/${this.toCsharpDay(dayOfWeek)}`);
  }

  addToSchedule(dayOfWeek: number, menuItemId: string) {
    return this.http.post<any>(this.baseUrl, { dayOfWeek: this.toCsharpDay(dayOfWeek), menuItemId });
  }

  removeFromSchedule(id: string) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }
}
