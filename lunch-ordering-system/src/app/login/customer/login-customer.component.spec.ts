import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LoginCustomerComponent } from './login-customer.component';

describe('Login', () => {
  let component: LoginCustomerComponent;
  let fixture: ComponentFixture<LoginCustomerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginCustomerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginCustomerComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
