import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MenuEditPanel } from './menu-edit-panel';

describe('MenuEditPanel', () => {
  let component: MenuEditPanel;
  let fixture: ComponentFixture<MenuEditPanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MenuEditPanel],
    }).compileComponents();

    fixture = TestBed.createComponent(MenuEditPanel);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
