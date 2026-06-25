import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RegisterCustomerComponent } from './register-customer-component';

describe('RegisterCustomerComponent', () => {
  let component: RegisterCustomerComponent;
  let fixture: ComponentFixture<RegisterCustomerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterCustomerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterCustomerComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
