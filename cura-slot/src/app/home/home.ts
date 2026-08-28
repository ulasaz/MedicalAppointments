import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { SplineBackgroundComponent } from '../shared/spline-background/spline-background.component';
import { TenantService } from '../services/tenant/tenant';

/**
 * Surface tone for a card — now only controls the colour of the soft glow behind
 * the icon. The card surface itself is the same frosted-glass treatment as the
 * navbar's pill islands (bg-white/40 backdrop-blur-lg border-white/40 shadow-xl
 * shadow-black/20) on every card, so the services grid and the navbar read as one
 * consistent design language instead of two unrelated colour systems.
 *
 * Tones are derived from the actual icon artwork rather than picked by eye. Each PNG's
 * chromatic pixels were bucketed into rose (hue >=300 or <30) and teal (150-210) and
 * weighted by saturation, giving these rose:teal ratios:
 *   dermatology (checkup icon) 3.96  <- the only genuinely rose-dominant icon (magnifier's warm pink body)
 *   orthopedics 1.78, genetics 1.36, gynecology 1.26, dental 1.19,
 *   cardiology 1.12, neurology 1.02
 *   pediatrics 0.86  <- the only icon that leans teal
 * Everything from ~0.95-1.8 is effectively an even pink/teal render, so it gets the
 * neutral blend instead of an arbitrary rose-or-teal label.
 */
type CardTone = 'featured' | 'rose' | 'teal' | 'balanced';

/** Frosted-glass card surface — identical treatment to the navbar's islands. */
const CARD_SURFACE =
  'bg-white/40 backdrop-blur-lg border-white/40 shadow-xl shadow-black/20 ' +
  'hover:bg-white/50';

/** Soft colour glow behind the icon, echoing the icon's own dominant hue. */
const TONE_GLOW: Record<CardTone, string> = {
  featured: 'bg-fuchsia-300/40',
  rose: 'bg-rose-400/40',
  teal: 'bg-teal-300/40',
  balanced: 'bg-fuchsia-200/40',
};

interface ServiceCard {
  id: string;
  iconSrc: string;
  titleKey: string;
  descriptionKey: string;
  ctaKey: string;
  ctaLink: string;
  /** Matched against DoctorProfile.specialization (stored in Polish) to pre-filter
   *  the search page. Omitted when no seeded specialty maps to this card, so the
   *  card still links somewhere real instead of a guaranteed-empty filtered list. */
  specialization?: string;
  featured?: boolean;
  /** Omit for the neutral blend; see CardTone for how these were measured. */
  tone?: Exclude<CardTone, 'featured'>;
}

interface Stat {
  valueKey: string;
  labelKey: string;
}

interface Doctor {
  photoSrc: string;
  nameKey: string;
  roleKey: string;
  bioKey: string;
}

interface Feature {
  titleKey: string;
  descriptionKey: string;
}

// One card per icon in public/images/icons/ — every asset in that folder is
// represented here, so adding a new icon means adding one entry, not a redesign.
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [TranslateModule, SplineBackgroundComponent, RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class HomePage {
  private router = inject(Router);
  tenantService = inject(TenantService);

  /** The center's own hero video/image wins over the default Spline animation. */
  get heroVideoUrl(): string | null {
    return this.tenantService.selectedTenant()?.bannerVideoUrl ?? null;
  }

  get heroImageUrl(): string | null {
    const center = this.tenantService.selectedTenant();
    return center?.hasBannerImage ? this.tenantService.bannerImageUrl(center.id) : null;
  }

  readonly stats: Stat[] = [
    { valueKey: 'HOME.STATS.DOCTORS.VALUE', labelKey: 'HOME.STATS.DOCTORS.LABEL' },
    { valueKey: 'HOME.STATS.APPOINTMENTS.VALUE', labelKey: 'HOME.STATS.APPOINTMENTS.LABEL' },
    { valueKey: 'HOME.STATS.SATISFACTION.VALUE', labelKey: 'HOME.STATS.SATISFACTION.LABEL' },
    { valueKey: 'HOME.STATS.SPECIALTIES.VALUE', labelKey: 'HOME.STATS.SPECIALTIES.LABEL' },
  ];

  readonly aboutFeatures: Feature[] = [
    { titleKey: 'HOME.ABOUT.FEATURE1.TITLE', descriptionKey: 'HOME.ABOUT.FEATURE1.DESCRIPTION' },
    { titleKey: 'HOME.ABOUT.FEATURE2.TITLE', descriptionKey: 'HOME.ABOUT.FEATURE2.DESCRIPTION' },
    { titleKey: 'HOME.ABOUT.FEATURE3.TITLE', descriptionKey: 'HOME.ABOUT.FEATURE3.DESCRIPTION' },
  ];

  readonly whyChoosePoints: string[] = [
    'HOME.WHY_CHOOSE.POINT1',
    'HOME.WHY_CHOOSE.POINT2',
    'HOME.WHY_CHOOSE.POINT3',
    'HOME.WHY_CHOOSE.POINT4',
  ];

  readonly doctors: Doctor[] = [
    {
      photoSrc: '/images/doctors/doctor-1.jpg',
      nameKey: 'HOME.MEET_DOCTORS.DOCTOR1.NAME',
      roleKey: 'HOME.MEET_DOCTORS.DOCTOR1.ROLE',
      bioKey: 'HOME.MEET_DOCTORS.DOCTOR1.BIO',
    },
    {
      photoSrc: '/images/doctors/doctor-2.jpg',
      nameKey: 'HOME.MEET_DOCTORS.DOCTOR2.NAME',
      roleKey: 'HOME.MEET_DOCTORS.DOCTOR2.ROLE',
      bioKey: 'HOME.MEET_DOCTORS.DOCTOR2.BIO',
    },
    {
      photoSrc: '/images/doctors/doctor-3.jpg',
      nameKey: 'HOME.MEET_DOCTORS.DOCTOR3.NAME',
      roleKey: 'HOME.MEET_DOCTORS.DOCTOR3.ROLE',
      bioKey: 'HOME.MEET_DOCTORS.DOCTOR3.BIO',
    },
  ];

  readonly serviceCards: ServiceCard[] = [
    {
      id: 'dermatology',
      iconSrc: '/images/icons/checkup.png',
      titleKey: 'HOME.SERVICES.DERMATOLOGY.TITLE',
      descriptionKey: 'HOME.SERVICES.DERMATOLOGY.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.DERMATOLOGY.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Dermatologia',
      // 3.96 rose:teal — by far the most rose-dominant icon in the set.
      tone: 'rose',
    },
    {
      id: 'dental',
      iconSrc: '/images/icons/dental.png',
      titleKey: 'HOME.SERVICES.DENTAL.TITLE',
      descriptionKey: 'HOME.SERVICES.DENTAL.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.DENTAL.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Stomatologia',
    },
    {
      id: 'cardiology',
      iconSrc: '/images/icons/cardiology.png',
      titleKey: 'HOME.SERVICES.CARDIOLOGY.TITLE',
      descriptionKey: 'HOME.SERVICES.CARDIOLOGY.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.CARDIOLOGY.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Kardiologia',
    },
    {
      id: 'pediatrics',
      iconSrc: '/images/icons/pediatrics.png',
      titleKey: 'HOME.SERVICES.PEDIATRICS.TITLE',
      descriptionKey: 'HOME.SERVICES.PEDIATRICS.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.PEDIATRICS.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Pediatria',
      // 0.86 rose:teal — the only icon in the set that leans teal.
      tone: 'teal',
    },
    {
      id: 'gynecology',
      iconSrc: '/images/icons/gynecology.png',
      titleKey: 'HOME.SERVICES.GYNECOLOGY.TITLE',
      descriptionKey: 'HOME.SERVICES.GYNECOLOGY.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.GYNECOLOGY.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Ginekologia',
    },
    {
      id: 'orthopedics',
      iconSrc: '/images/icons/orthopedics.png',
      titleKey: 'HOME.SERVICES.ORTHOPEDICS.TITLE',
      descriptionKey: 'HOME.SERVICES.ORTHOPEDICS.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.ORTHOPEDICS.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Ortopedia',
    },
    {
      id: 'neurology',
      iconSrc: '/images/icons/neurology.png',
      titleKey: 'HOME.SERVICES.NEUROLOGY.TITLE',
      descriptionKey: 'HOME.SERVICES.NEUROLOGY.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.NEUROLOGY.CTA',
      ctaLink: '/doctors/search',
      specialization: 'Neurologia',
    },
    {
      id: 'genetics',
      iconSrc: '/images/icons/genetics.png',
      titleKey: 'HOME.SERVICES.GENETICS.TITLE',
      descriptionKey: 'HOME.SERVICES.GENETICS.DESCRIPTION',
      ctaKey: 'HOME.SERVICES.GENETICS.CTA',
      ctaLink: '/doctors/search',
      // No seeded doctor covers genetics yet — links to the unfiltered list rather
      // than a specialization value that would always show zero results.
    },
  ];

  /** [routerLink]/[queryParams] pair for a service card's CTA. */
  queryParamsFor(card: ServiceCard): { specialization: string } | {} {
    return card.specialization ? { specialization: card.specialization } : {};
  }

  private toneOf(card: ServiceCard): CardTone {
    return card.featured ? 'featured' : (card.tone ?? 'balanced');
  }

  /** Colour of the blurred bloom sitting behind the icon. */
  glowClass(card: ServiceCard): string {
    return TONE_GLOW[this.toneOf(card)];
  }

  goToSearch() {
    this.router.navigate(['/doctors/search']);
  }
}