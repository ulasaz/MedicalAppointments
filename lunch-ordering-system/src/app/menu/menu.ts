import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { MenuService } from '../services/menu/menu';
import { CommonModule } from '@angular/common';
import { CategoriesService } from '../services/categories/categories';
import { Router } from '@angular/router';
import { OrdersService } from '../services/orders/orders';
import { ToastService } from '../services/toast/toast';
import { AuthService } from '../services/auth/auth';
import { PricePipe } from '../pipes/price-pipe';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [CommonModule, PricePipe, ReactiveFormsModule, TranslatePipe],
  templateUrl: './menu.html',
  styleUrl: './menu.css',
})
export class Menu implements OnInit {

  private menuService = inject(MenuService);
  private categoryService = inject(CategoriesService);
  private ordersService = inject(OrdersService);
  public toastService = inject(ToastService);
  public authService = inject(AuthService);
  private translateService = inject(TranslateService);

  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);

  menuItems: any[] = [];
  categories: any[] = [];
  filteredMenuItems: any[] = this.menuItems;
  selectedCategory: any = null;
  cartItems: any[] = [];

  isAddModalOpen = false;
  isEditModalOpen = false;
  isDeleteModalOpen = false;
  selectedMenuIdToEdit: string | null = null;
  selectedMenuToDelete: { id: string; name: string } | null = null;

  public itemForm = new FormGroup({
    categoryName: new FormControl('', [Validators.required]),
    name: new FormControl('', [Validators.required]),
    description: new FormControl('', [Validators.required]),
    priceMinor: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0)
    ]),
    isAvailable: new FormControl<boolean>(true),
    photoUrl: new FormControl<string | null>(null)
  });

  ngOnInit() {
    this.loadCategories();
    this.loadMenu();
    this.getItemsFromCart();
  }

  loadMenu() {
    const request = this.authService.getUserRole() === 'Cafe'
      ? this.menuService.getAllMenuItemsUnfiltered()
      : this.menuService.getAllMenuItems();
    request.subscribe({
      next: (response: any) => {
        this.menuItems = response;
        this.cdr.markForCheck();
        this.filteredMenuItems = response;
      },
      error: (err) => {
        console.error('Error during loading menu', err);
      }
    });
  }

  loadCategories() {
    this.categoryService.getAllCategories().subscribe({
      next: (response: any) => {
        this.categories = response;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Error during loading category', err);
      }
    });
  }

  selectCategory(category: any) {
    if (this.selectedCategory === category) {
      this.selectedCategory = null;
      this.filteredMenuItems = this.menuItems;
      this.router.navigate(['/menu']);
    } else {
      this.selectedCategory = category;
      this.filteredMenuItems = this.menuItems.filter(item => item.categoryId === category.id);
      this.router.navigate(['/menu'], { queryParams: { category: category.name } });
    }
  }

  selectAll() {
    this.selectedCategory = null;
    this.filteredMenuItems = this.menuItems;
    this.router.navigate(['/menu']);
  }

  addToCart(item: any) {
    if (this.authService.isAuthenticated()) {
      const savedCart = localStorage.getItem('cart') ?? '[]';
      let cartItems = JSON.parse(savedCart);
      const existingItemIndex = cartItems.findIndex((cartItem: any) => cartItem.id === item.id);
      if (existingItemIndex !== -1) {
        cartItems[existingItemIndex].quantity += 1;
      } else {
        item.quantity = 1;
        cartItems.push(item);
      }
      localStorage.setItem('cart', JSON.stringify(cartItems));
      this.toastService.info(this.translateService.instant('MENU.TOAST.ITEM_ADDED_TO_CART'));
      this.getItemsFromCart();
    } else {
      this.toastService.error(this.translateService.instant('MENU.TOAST.LOGIN_REQUIRED'));
      this.router.navigate(['/login']);
    }
  }

  getItemsFromCart() {
    const savedCart = localStorage.getItem('cart');
    this.cartItems = savedCart !== null ? JSON.parse(savedCart) : [];
  }

  increaseQuantity(item: any) {
    const index = this.cartItems.findIndex((cartItem: any) => cartItem.id === item.id);
    if (index !== -1) {
      this.cartItems[index].quantity += 1;
      localStorage.setItem('cart', JSON.stringify(this.cartItems));
    }
  }

  decreaseQuantity(item: any) {
    const index = this.cartItems.findIndex((cartItem: any) => cartItem.id === item.id);
    if (index !== -1) {
      this.cartItems[index].quantity -= 1;
      if (this.cartItems[index].quantity === 0) {
        this.cartItems.splice(index, 1);
      }
      localStorage.setItem('cart', JSON.stringify(this.cartItems));
    }
  }

  get cartTotalAmount(): number {
    return this.cartItems.reduce((sum, item) => sum + (item.priceMinor * item.quantity), 0);
  }

  placeOrder(cart: any[]) {
    if (this.authService.isAuthenticated()) {
      const orderPayload = {
        note: '',
        lines: cart.map(item => ({ menuItemId: item.id, quantity: item.quantity }))
      };
      this.ordersService.placeNewOrder(orderPayload).subscribe({
        next: () => {
          localStorage.removeItem('cart');
          this.cartItems = [];
          this.router.navigate(['/orders']);
          this.toastService.success(this.translateService.instant('MENU.TOAST.ORDER_PLACED'));
        },
        error: () => {
          this.toastService.error(this.translateService.instant('MENU.TOAST.ORDER_FAILED'));
        }
      });
    }
  }

  openAddModal() {
    this.itemForm.reset({ isAvailable: true });
    this.isAddModalOpen = true;
  }

  addItem() {
    if (!this.itemForm.valid) return;
    this.menuService.addItem(this.itemForm.value).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('MENU.TOAST.ITEM_ADDED'));
        this.closeAddModal();
        this.loadMenu();
      },
      error: (err) => {
        console.error('Error during adding item:', err);
        this.toastService.error(this.translateService.instant('MENU.TOAST.ITEM_ADD_ERROR'));
      }
    });
  }

  closeAddModal() {
    this.isAddModalOpen = false;
  }

  openEditModal(item: any) {
    this.selectedMenuIdToEdit = item.id;
    this.itemForm.patchValue({
      name: item.name,
      description: item.description,
      categoryName: item.categoryId ?? '',
      priceMinor: item.priceMinor,
      isAvailable: item.isAvailable,
      photoUrl: item.photoUrl ?? null,
    });
    this.isEditModalOpen = true;
  }

  editItem() {
    if (!this.itemForm.valid || !this.selectedMenuIdToEdit) return;
    this.menuService.updateItem(this.selectedMenuIdToEdit, this.itemForm.value).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('MENU.TOAST.ITEM_UPDATED'));
        this.closeEditModal();
        this.loadMenu();
      },
      error: (err) => {
        console.error('Error during updating item:', err);
        this.toastService.error(this.translateService.instant('MENU.TOAST.ITEM_UPDATE_ERROR'));
      }
    });
  }

  openDeleteModal(item: any) {
    this.selectedMenuToDelete = { id: item.id, name: item.name };
    this.isDeleteModalOpen = true;
  }

  closeDeleteModal() {
    this.isDeleteModalOpen = false;
    this.selectedMenuToDelete = null;
  }

  confirmDelete() {
    if (!this.selectedMenuToDelete) return;
    this.menuService.deleteItem(this.selectedMenuToDelete.id).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant('MENU.TOAST.ITEM_DELETED'));
        this.closeDeleteModal();
        this.loadMenu();
      },
      error: (err) => {
        console.error('Error during deleting item:', err);
        this.toastService.error(this.translateService.instant('MENU.TOAST.ITEM_DELETE_ERROR'));
      }
    });
  }

  toggleAvailability(item: any) {
    this.menuService.toggleAvailability(item.id, !item.isAvailable).subscribe({
      next: () => {
        this.toastService.info(this.translateService.instant(
          item.isAvailable ? 'MENU.TOAST.MARKED_SOLD_OUT' : 'MENU.TOAST.MARKED_AVAILABLE'
        ));
        this.loadMenu();
      },
      error: (err) => {
        console.error('Error toggling availability:', err);
        this.toastService.error(this.translateService.instant('MENU.TOAST.AVAILABILITY_ERROR'));
      }
    });
  }

  closeEditModal() {
    this.isEditModalOpen = false;
    this.selectedMenuIdToEdit = null;
  }
}
