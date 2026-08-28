import { Component, Input, inject } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'app-spline-background',
  standalone: true,
  templateUrl: './spline-background.component.html',
  styleUrl: './spline-background.component.css',
})
export class SplineBackgroundComponent {
  private sanitizer = inject(DomSanitizer);

  @Input() url = 'https://my.spline.design/bloodcells-zMrfmcm7OMRcLLJpEB6ZGmPm/';

  get safeUrl() {
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.url);
  }
}
