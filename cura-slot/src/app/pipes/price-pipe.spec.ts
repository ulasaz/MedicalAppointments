import { PricePipe } from './price-pipe';

describe('PricePipe', () => {
  let pipe: PricePipe;

  beforeEach(() => {
    pipe = new PricePipe();
  });

  it('should create an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('should convert minor units to major units with default $ symbol', () => {
    expect(pipe.transform(1500)).toBe('15.00$');
  });

  it('should handle single-digit cents correctly', () => {
    expect(pipe.transform(101)).toBe('1.01$');
  });

  it('should format zero as 0.00', () => {
    expect(pipe.transform(0)).toBe('0.00$');
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null as any)).toBe('');
  });

  it('should return empty string for undefined', () => {
    expect(pipe.transform(undefined as any)).toBe('');
  });

  it('should use a custom currency symbol', () => {
    expect(pipe.transform(1500, 'zł')).toBe('15.00zł');
  });

  it('should handle negative values', () => {
    expect(pipe.transform(-150)).toBe('-1.50$');
  });
});
