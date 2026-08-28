import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { DoctorDashboard } from './doctor-dashboard';

describe('DoctorDashboard', () => {
  let component: DoctorDashboard;
  let fixture: ComponentFixture<DoctorDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DoctorDashboard],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(DoctorDashboard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
