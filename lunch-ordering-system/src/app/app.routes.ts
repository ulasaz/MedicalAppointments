import { Routes } from '@angular/router';
import { LoginCustomerComponent } from './login/customer/login-customer.component';
import { Menu } from './menu/menu';
import { Orders } from './orders/orders';
import { CafePanel } from './cafe-panel/cafe-panel';
import { roleGuard } from './guards/role.guard';
import { RegisterCustomerComponent } from './register/register-customer-component/register-customer-component';

export const routes: Routes = [{ 
    path: 'login',
    component: LoginCustomerComponent
  },
  {
    path: 'menu',
    component: Menu
  },
  {
    path: 'orders',
    component: Orders,
    canActivate: [roleGuard('Customer')]
  },
    {
    path: 'cafe-panel',
    component: CafePanel,
    canActivate: [roleGuard('Cafe')]
  },
  {
    path: 'register',
    component: RegisterCustomerComponent
  },
  {
    path: '',
    redirectTo: 'menu',
    pathMatch: 'full'
  }];
