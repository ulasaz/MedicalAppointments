import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { DoctorProfile, DoctorsService } from '../services/doctors/doctors';
import { AppointmentsService, DayAvailability, VisitType } from '../services/appointments/appointments';
import { PricePipe } from '../pipes/price-pipe';
import { DoctorPhotoUrlPipe } from '../pipes/doctor-photo-url-pipe';
import { initials } from '../shared/initials';

interface DoctorCategory {
  specialization: string;
  doctors: DoctorProfile[];
}

type SortOption = 'name' | 'rating' | 'price';
type AvailabilityFilter = 'any' | 'today' | 'week';

interface NearestSlot {
  date: string;
  start: Date;
}

interface FreeInterval {
  start: Date;
  end: Date;
}

/** Avatar background ramp — same rose/fuchsia/teal family used across the app,
 *  cycled by name so the list doesn't render every avatar in the same tone. */
const AVATAR_TONES = [
  'from-rose-400 to-fuchsia-400',
  'from-fuchsia-400 to-teal-400',
  'from-teal-400 to-rose-400',
];

/** How many days ahead to probe for the next open slot before giving up. */
const NEAREST_SLOT_LOOKAHEAD_DAYS = 7;

/** Mirrors AppointmentRules:MinLeadTimeMinutes on the server. */
const BOOKING_LEAD_MINUTES = 30;

/** Advertised start times are snapped to this grid, like a real booking calendar. */
const SLOT_GRID_MINUTES = 15;

function ceilToGrid(date: Date): Date {
  const d = new Date(date);
  d.setSeconds(0, 0);
  const remainder = d.getMinutes() % SLOT_GRID_MINUTES;
  if (remainder !== 0) d.setMinutes(d.getMinutes() + (SLOT_GRID_MINUTES - remainder));
  return d;
}

@Component({
  selector: 'app-doctors',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, RouterLink, PricePipe, DoctorPhotoUrlPipe],
  templateUrl: './doctors.html',
  styleUrl: './doctors.css',
})
export class Doctors implements OnInit {
  private doctorsService = inject(DoctorsService);
  private appointmentsService = inject(AppointmentsService);
  private route = inject(ActivatedRoute);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);

  doctors: DoctorProfile[] = [];
  isLoading = false;
  hasLoaded = false;
  selectedCategory: string | null = null;
  nameQuery = '';
  cityFilter: string | null = null;
  sortBy: SortOption = 'name';
  availabilityFilter: AvailabilityFilter = 'any';
  showFilters = false;

  /** undefined = still checking, null = nothing found within the lookahead window. */
  nearestSlots: Record<string, NearestSlot | null | undefined> = {};

  get categories(): DoctorCategory[] {
    const groups = new Map<string, DoctorProfile[]>();
    for (const doctor of this.doctors) {
      const key = doctor.specialization || '—';
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(doctor);
    }
    return Array.from(groups.entries())
      .map(([specialization, doctors]) => ({ specialization, doctors }))
      .sort((a, b) => a.specialization.localeCompare(b.specialization));
  }

  get cities(): string[] {
    return Array.from(new Set(this.doctors.map((d) => d.city).filter(Boolean))).sort((a, b) => a.localeCompare(b));
  }

  get filteredDoctors(): DoctorProfile[] {
    const query = this.nameQuery.trim().toLowerCase();
    const filtered = this.doctors.filter((doctor) => {
      const matchesCategory = !this.selectedCategory || doctor.specialization === this.selectedCategory;
      const matchesQuery = !query || doctor.fullName.toLowerCase().includes(query);
      const matchesCity = !this.cityFilter || doctor.city === this.cityFilter;
      return matchesCategory && matchesQuery && matchesCity && this.matchesAvailability(doctor);
    });

    const sorted = [...filtered];
    if (this.sortBy === 'rating') {
      sorted.sort((a, b) => (b.averageRating ?? -1) - (a.averageRating ?? -1));
    } else if (this.sortBy === 'price') {
      sorted.sort((a, b) => {
        const pa = this.startingPriceCents(a);
        const pb = this.startingPriceCents(b);
        if (pa == null) return 1;
        if (pb == null) return -1;
        return pa - pb;
      });
    } else {
      sorted.sort((a, b) => a.fullName.localeCompare(b.fullName));
    }
    return sorted;
  }

  selectCategory(specialization: string | null) {
    this.selectedCategory = specialization;
  }

  toggleFilters() {
    this.showFilters = !this.showFilters;
  }

  selectCity(city: string | null) {
    this.cityFilter = city;
  }

  setSort(sort: SortOption) {
    this.sortBy = sort;
  }

  setAvailabilityFilter(filter: AvailabilityFilter) {
    this.availabilityFilter = filter;
  }

  /** "Today"/"This week" only make sense once we know each doctor's nearest slot,
   *  which loads asynchronously (loadNearestSlots) — a doctor still `undefined`
   *  (not yet resolved) is kept in the list rather than hidden, so the results
   *  don't flicker down to empty while slots are still being probed. */
  private matchesAvailability(doctor: DoctorProfile): boolean {
    if (this.availabilityFilter === 'any') return true;

    const slot = this.nearestSlots[doctor.id];
    if (slot === undefined) return true;
    if (slot === null) return false;
    if (this.availabilityFilter === 'week') return true; // lookahead window IS one week

    return slot.start.toDateString() === new Date().toDateString();
  }

  startingPriceCents(doctor: DoctorProfile): number | null {
    const prices = [doctor.priceStationaryCents, doctor.priceOnlineCents]
      .filter((price): price is number => price != null);
    return prices.length > 0 ? Math.min(...prices) : null;
  }

  /** Visit types the doctor actually offers, derived from which prices are set. */
  visitTypes(doctor: DoctorProfile): VisitType[] {
    const types: VisitType[] = [];
    if (doctor.priceStationaryCents != null) types.push('Stationary');
    if (doctor.priceOnlineCents != null) types.push('Online');
    return types;
  }

  readonly initials = initials;

  avatarTone(doctor: DoctorProfile): string {
    let hash = 0;
    for (let i = 0; i < doctor.id.length; i++) hash = (hash * 31 + doctor.id.charCodeAt(i)) >>> 0;
    return AVATAR_TONES[hash % AVATAR_TONES.length];
  }

  /** Short day label for the calendar chip: "Today" / "Tomorrow" / "05 Aug". */
  slotDayShort(doctorId: string): string {
    const slot = this.nearestSlots[doctorId];
    if (!slot) return '';

    const now = new Date();
    if (slot.start.toDateString() === now.toDateString()) {
      return this.translate.instant('DOCTORS.SLOT_TODAY');
    }
    const tomorrow = new Date(now);
    tomorrow.setDate(now.getDate() + 1);
    if (slot.start.toDateString() === tomorrow.toDateString()) {
      return this.translate.instant('DOCTORS.SLOT_TOMORROW');
    }

    const locale = this.translate.currentLang === 'pl' ? 'pl-PL' : 'en-US';
    return slot.start.toLocaleDateString(locale, { day: '2-digit', month: 'short' });
  }

  /** Time half of the calendar chip, e.g. "14:30". */
  slotTime(doctorId: string): string {
    const slot = this.nearestSlots[doctorId];
    if (!slot) return '';
    const locale = this.translate.currentLang === 'pl' ? 'pl-PL' : 'en-US';
    return slot.start.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
  }

  private async loadNearestSlots() {
    await Promise.all(this.doctors.map(async (doctor) => {
      this.nearestSlots[doctor.id] = undefined;
      const slot = await this.findNearestSlot(doctor.id);
      this.nearestSlots[doctor.id] = slot;
      this.cdr.markForCheck();
    }));
  }

  private async findNearestSlot(doctorId: string): Promise<NearestSlot | null> {
    const today = new Date();
    // A slot the patient couldn't actually book (inside the lead-time window) is
    // misleading here, so mirror the server's booking rule and start from the first
    // bookable moment rounded up to a whole quarter-hour.
    const earliest = new Date(today.getTime() + BOOKING_LEAD_MINUTES * 60_000);
    earliest.setSeconds(0, 0);
    const remainder = earliest.getMinutes() % SLOT_GRID_MINUTES;
    if (remainder !== 0) earliest.setMinutes(earliest.getMinutes() + (SLOT_GRID_MINUTES - remainder));

    for (let offset = 0; offset < NEAREST_SLOT_LOOKAHEAD_DAYS; offset++) {
      const day = new Date(today);
      day.setDate(today.getDate() + offset);
      const dateStr = day.toISOString().slice(0, 10);

      let availability: DayAvailability;
      try {
        availability = await firstValueFrom(this.appointmentsService.getAvailableSlots(doctorId, dateStr));
      } catch {
        continue;
      }

      const free = this.freeIntervals(availability, earliest);
      if (free.length > 0) {
        return { date: dateStr, start: free[0].start };
      }
    }
    return null;
  }

  /** Working windows minus booked ranges, clipped to not start before `notBefore`. */
  private freeIntervals(availability: DayAvailability, notBefore: Date | null): FreeInterval[] {
    const booked = availability.bookedRanges
      .map((r) => ({ start: new Date(r.startTime), end: new Date(r.endTime) }))
      .sort((a, b) => a.start.getTime() - b.start.getTime());

    const free: FreeInterval[] = [];
    for (const window of availability.workingWindows) {
      let cursor = new Date(window.startTime);
      const windowEnd = new Date(window.endTime);

      for (const b of booked) {
        if (b.end <= cursor || b.start >= windowEnd) continue;
        if (b.start > cursor) free.push({ start: new Date(cursor), end: new Date(b.start) });
        if (b.end > cursor) cursor = new Date(b.end);
      }
      if (cursor < windowEnd) free.push({ start: cursor, end: windowEnd });
    }

    return free
      .map((f) => (notBefore && f.start < notBefore ? { start: notBefore, end: f.end } : f))
      // Snap to the quarter-hour grid so a slot freed at e.g. 14:07 advertises 14:15.
      .map((f) => ({ start: ceilToGrid(f.start), end: f.end }))
      .filter((f) => f.start < f.end)
      .sort((a, b) => a.start.getTime() - b.start.getTime());
  }

  ngOnInit() {
    // Read filters from the URL reactively (not just on first load) so links like the
    // home page's specialization cards or the navbar's "Top rated" shortcut work even
    // when the user is already sitting on this page.
    this.route.queryParamMap.subscribe((params) => {
      this.selectedCategory = params.get('specialization');
      const sort = params.get('sort');
      this.sortBy = sort === 'rating' || sort === 'price' ? sort : 'name';
      this.cdr.markForCheck();
    });

    this.isLoading = true;
    this.doctorsService.search({}).subscribe({
      next: (response) => {
        this.doctors = response;
        this.isLoading = false;
        this.hasLoaded = true;
        this.cdr.markForCheck();
        void this.loadNearestSlots();
      },
      error: (err) => {
        console.error('Error loading doctors', err);
        this.isLoading = false;
        this.hasLoaded = true;
        this.cdr.markForCheck();
      },
    });
  }
}
