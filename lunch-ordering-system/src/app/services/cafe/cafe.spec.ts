import { TestBed } from '@angular/core/testing';

import { CafeService } from './cafe';

describe('Cafe', () => {
  let service: CafeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CafeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
