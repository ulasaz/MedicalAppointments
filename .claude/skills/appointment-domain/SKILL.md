---
name: appointment-domain
description: Use whenever writing or modifying code in Doctors.API, Appointments.API, Identity.API roles, or any scheduling/booking logic in this repo. Encodes the domain model, roles, and business rules for the appointment-booking system (a Znany-Lekarz-style doctor search & booking platform) so implementations stay consistent across sessions and don't drift back toward the old LunchOrderingSystem's menu/order concepts.
---

# Appointment Booking Domain

This service is a doctor-search-and-booking platform (think Znany Lekarz / ZocDoc / Doctolib), rebuilt from the LunchOrderingSystem codebase. The infrastructure (Gateway, RabbitMQ, per-service Postgres, Docker Compose) is reused. The domain logic below is new and must not just be a renamed copy of the old Menu/Order domain.

## Roles

- `Patient` — searches doctors, books/cancels appointments, views own visit history.
- `Doctor` — manages own profile and schedule, confirms/cancels appointments booked with them.
- `Admin` — user/role management only. Not part of the core booking flow.

Every endpoint that touches an `Appointment` or `DoctorProfile` must check both the role AND resource ownership (a patient can only cancel their own appointment; a doctor can only confirm/cancel appointments booked with them).

## Core entities (build these first — this is the required scope)

**DoctorProfile** (lives in `Doctors.API`)
- `Id`, `UserId` (FK → Identity), `FullName`, `Specialization`, `City`, `Description`, `IsActive`

**AvailabilitySlot / Schedule** (working hours a doctor exposes)
- `DoctorId`, day/date, `StartTime`, `EndTime`
- This has no equivalent in the old Menu.API — do not try to model it as a "menu item availability" flag. It needs its own concept of recurring or dated open windows.

**Appointment** (lives in `Appointments.API`)
- `Id`, `PatientId`, `DoctorId`, `StartTime`, `EndTime`, `Status`, `CreatedAt`
- `Status`: `Requested` → `Confirmed` → `Completed`, or → `Cancelled` from either of the first two states.

## Business rules (non-negotiable — write a unit test for each)

1. **Duration bounds**: appointment length must be within a configured min/max (default 15–120 minutes). Make this a named constant/config value, not a magic number inline.
2. **Lead time**: `StartTime` must be at least a configured minimum (default 30 minutes) after "now." "Now" always comes from an injected `IClock`, never `DateTime.Now`/`DateTime.UtcNow` directly — this is what makes rule testing deterministic.
3. **Doctor must be active**: `DoctorProfile.IsActive` must be true to accept new bookings.
4. **No overlap, boundary touching allowed**: two appointments for the same doctor must not overlap. An appointment ending exactly when another starts is allowed (touching boundaries are not an overlap).
5. **No double-cancel**: cancelling an already-`Cancelled` appointment is rejected, not silently accepted.
6. **Confirm is doctor-only**: only the assigned doctor can move `Requested` → `Confirmed`.
7. **Cancel is patient-or-doctor**: either party on the appointment can cancel; nobody else can.

## Endpoint shape (keep consistent across services)

- `GET /doctors?specialization=&city=&name=` — search
- `GET /doctors/{id}` — profile detail
- `PUT /doctors/{id}` — doctor edits own profile
- `GET /doctors/{id}/available-slots?date=` — computed from schedule minus existing bookings
- `POST /appointments` — book (runs all rules above)
- `PUT /appointments/{id}/confirm` — doctor only
- `PUT /appointments/{id}/cancel` — patient or doctor
- `GET /appointments?patientId=` / `?doctorId=` — history

## Testing convention (carry over from the existing test practice in this repo)

- Every business rule above gets its own unit test, including the edge case (e.g., an appointment that touches a boundary exactly should NOT be rejected as an overlap).
- Use `IClock` mocking to control "now" in tests instead of relying on real time.
- After writing a rule, deliberately break the implementation and confirm the test fails (mutation-testing habit) before trusting it's a real test and not a tautology.

## Optional extensions (only after core scope above is done and tested — these push the "Znany Lekarz" resemblance further but are NOT part of the approved thesis scope, so treat them as stretch goals)

- `VisitType` on `Appointment`: `Stationary` / `Online`
- `Review` entity: rating (1–5) + comment, tied to a `Completed` appointment, one review per appointment
- Price per visit type on `DoctorProfile`

## Naming discipline

Do not let old vocabulary leak in from LunchOrderingSystem: no `Menu`, `Order`, `Cart`, `Item`, `Category` in new code — even as internal variable names. If you're renaming a file and a term like this shows up, it's a sign that entity needs a real redesign, not a find-and-replace.