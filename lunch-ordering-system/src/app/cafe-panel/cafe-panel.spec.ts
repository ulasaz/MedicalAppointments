import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CafePanel } from './cafe-panel';

describe('CafePanel', () => {
  let component: CafePanel;
  let fixture: ComponentFixture<CafePanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CafePanel],
    }).compileComponents();

    fixture = TestBed.createComponent(CafePanel);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
