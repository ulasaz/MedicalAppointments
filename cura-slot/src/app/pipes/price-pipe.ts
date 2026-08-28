import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'price',
  standalone: true
})
export class PricePipe implements PipeTransform {

  transform(value: number, currencySymbol: string = '$'): string {
    
    if (value === null || value === undefined) {
      return '';
    }

    const majorUnit = value / 100;

return `${majorUnit.toFixed(2)}${currencySymbol}`;

  }
}