import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './language-switcher.component.html',
})
export class LanguageSwitcherComponent implements OnInit, OnDestroy {
  private translate = inject(TranslateService);

  currentLang = 'en';
  private langSub!: Subscription;

  ngOnInit() {
    this.currentLang = localStorage.getItem('language') ?? 'en';
    this.langSub = this.translate.onLangChange.subscribe(({ lang }) => {
      this.currentLang = lang;
    });
  }

  ngOnDestroy() {
    this.langSub?.unsubscribe();
  }

  switchLang(lang: string) {
    this.translate.use(lang);
    localStorage.setItem('language', lang);
  }
}
