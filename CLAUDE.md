# LunchOrderingSystem → Appointment Booking Rebuild

This repo is being rebuilt from a lunch-ordering microservices app (.NET + Angular)
into a doctor-search-and-booking platform (Znany Lekarz style), for an engineering
diploma thesis.

- Domain rules & entity model: see `.claude/skills/appointment-domain/SKILL.md`
- Full rebuild plan & phase order: see `docs/rebuild-plan.md`
- Infra (Gateway, RabbitMQ, per-service Postgres, Docker Compose) is reused as-is.
  Domain logic (Doctors.API, Appointments.API, roles) is being redesigned from
  scratch — not a renamed copy of the old Menu/Order domain.