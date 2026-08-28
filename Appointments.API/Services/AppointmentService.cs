using Appointments.Database;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Services;

public class AppointmentService : IAppointmentService
{
    private const int DefaultMinDurationMinutes = 15;
    private const int DefaultMaxDurationMinutes = 120;
    private const int DefaultMinLeadTimeMinutes = 30;

    private readonly DatabaseContext _dbContext;
    private readonly IDoctorsClient _doctorsClient;
    private readonly IIdentityClient _identityClient;
    private readonly IClock _clock;
    private readonly IConfiguration _config;
    private readonly IPublishEndpoint _publishEndpoint;

    public AppointmentService(
        DatabaseContext dbContext,
        IDoctorsClient doctorsClient,
        IIdentityClient identityClient,
        IClock clock,
        IConfiguration config,
        IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _doctorsClient = doctorsClient;
        _identityClient = identityClient;
        _clock = clock;
        _config = config;
        _publishEndpoint = publishEndpoint;
    }
    public async Task<DayAvailabilityDto> GetAvailableSlotsAsync(Guid doctorId, DateOnly date)
    {
        var doctor = await _doctorsClient.GetDoctorAsync(doctorId);
        if (doctor == null)
        {
            throw new ArgumentException("Doctor not found");
        }

        var workingWindows = await _dbContext.AvailabilitySlots
            .Where(s => s.DoctorId == doctorId && s.Date == date)
            .OrderBy(s => s.StartTime)
            .AsNoTracking()
            .ToListAsync();

        if (workingWindows.Count == 0)
        {
            return new DayAvailabilityDto(); // doctor doesn't work this day
        }

        var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEndExclusive = dayStart.AddDays(1);

        var bookedAppointments = await _dbContext.Appointments
            .Where(a => a.DoctorId == doctorId
                        && a.StartTime < dayEndExclusive
                        && a.EndTime > dayStart
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.Rejected)
            .OrderBy(a => a.StartTime)
            .AsNoTracking()
            .ToListAsync();

        return new DayAvailabilityDto
        {
            WorkingWindows = workingWindows
                .Select(w => new SlotDto
                {
                    StartTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.FromTimeSpan(w.StartTime)), DateTimeKind.Utc),
                    EndTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.FromTimeSpan(w.EndTime)), DateTimeKind.Utc),
                    AllowedVisitTypes = w.AllowedVisitTypes
                })
                .ToList(),
            BookedRanges = bookedAppointments
                .Select(a => new SlotDto { StartTime = a.StartTime, EndTime = a.EndTime })
                .ToList()
        };
    }

    public async Task<List<AvailabilitySlotDto>> GetScheduleAsync(Guid doctorId)
    {
        return await _dbContext.AvailabilitySlots
            .Where(s => s.DoctorId == doctorId)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new AvailabilitySlotDto
            {
                Id = s.Id,
                DoctorId = s.DoctorId,
                Date = s.Date,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                AllowedVisitTypes = s.AllowedVisitTypes
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<AvailabilitySlotDto> AddScheduleSlotAsync(Guid requestingUserId, Guid doctorId, AvailabilitySlotCreateDto dto)
    {
        var doctor = await _doctorsClient.GetDoctorAsync(doctorId);
        if (doctor == null)
        {
            throw new ArgumentException("Doctor not found");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can manage this schedule");
        }

        if (dto.StartTime >= dto.EndTime)
        {
            throw new ArgumentException("StartTime must be before EndTime");
        }

        if (dto.Date < DateOnly.FromDateTime(_clock.UtcNow))
        {
            throw new ArgumentException("Cannot add availability in the past");
        }

        var overlaps = await _dbContext.AvailabilitySlots
            .AnyAsync(s => s.DoctorId == doctorId
                           && s.Date == dto.Date
                           && s.StartTime < dto.EndTime
                           && s.EndTime > dto.StartTime);
        if (overlaps)
        {
            throw new InvalidOperationException("This window overlaps with an existing schedule window");
        }

        var allowedVisitTypes = dto.AllowedVisitTypes is { Count: > 0 }
            ? dto.AllowedVisitTypes.Distinct().ToList()
            : new List<VisitType> { VisitType.Stationary, VisitType.Online };

        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = dto.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            AllowedVisitTypes = allowedVisitTypes
        };

        _dbContext.AvailabilitySlots.Add(slot);
        await _dbContext.SaveChangesAsync();

        return new AvailabilitySlotDto
        {
            Id = slot.Id,
            DoctorId = slot.DoctorId,
            Date = slot.Date,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            AllowedVisitTypes = slot.AllowedVisitTypes
        };
    }

    public async Task DeleteScheduleSlotAsync(Guid requestingUserId, Guid doctorId, Guid slotId)
    {
        var doctor = await _doctorsClient.GetDoctorAsync(doctorId);
        if (doctor == null)
        {
            throw new ArgumentException("Doctor not found");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can manage this schedule");
        }

        var slot = await _dbContext.AvailabilitySlots.FirstOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctorId);
        if (slot == null)
        {
            throw new ArgumentException("Schedule window not found");
        }

        _dbContext.AvailabilitySlots.Remove(slot);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<AppointmentInfoDto> BookAsync(Guid patientId, AppointmentCreateDto dto)
    {
        var minDuration = TimeSpan.FromMinutes(_config.GetValue("AppointmentRules:MinDurationMinutes", DefaultMinDurationMinutes));
        var maxDuration = TimeSpan.FromMinutes(_config.GetValue("AppointmentRules:MaxDurationMinutes", DefaultMaxDurationMinutes));
        var minLeadTime = TimeSpan.FromMinutes(_config.GetValue("AppointmentRules:MinLeadTimeMinutes", DefaultMinLeadTimeMinutes));

        var duration = TimeSpan.FromMinutes(dto.DurationMinutes);
        if (duration < minDuration || duration > maxDuration)
        {
            throw new ArgumentException(
                $"Appointment duration must be between {minDuration.TotalMinutes} and {maxDuration.TotalMinutes} minutes.");
        }

        var startTime = DateTime.SpecifyKind(dto.StartTime, DateTimeKind.Utc);

        if (startTime < _clock.UtcNow.Add(minLeadTime))
        {
            throw new ArgumentException(
                $"Appointment start time must be at least {minLeadTime.TotalMinutes} minutes from now.");
        }

        var doctor = await _doctorsClient.GetDoctorAsync(dto.DoctorId);
        if (doctor == null)
        {
            throw new ArgumentException("Doctor not found");
        }

        if (!doctor.IsActive)
        {
            throw new InvalidOperationException("Doctor is not currently accepting bookings");
        }

        MedicalServiceInfoDto? service = null;
        if (dto.MedicalServiceId.HasValue)
        {
            service = await _doctorsClient.GetServiceAsync(dto.DoctorId, dto.MedicalServiceId.Value);
            if (service == null)
            {
                throw new ArgumentException("Service not found");
            }

            if (!service.AllowedVisitTypes.Contains(dto.VisitType))
            {
                throw new ArgumentException("This service is not available for the selected visit type");
            }
        }

        int? priceCents = service != null
            ? service.PriceCents
            : dto.VisitType switch
            {
                VisitType.Stationary => doctor.PriceStationaryCents,
                VisitType.Online => doctor.PriceOnlineCents,
                _ => null
            };
        if (priceCents == null)
        {
            throw new ArgumentException("Doctor does not offer this visit type");
        }

        var endTime = startTime + duration;

        var overlappingWindow = await _dbContext.AvailabilitySlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId
                                       && s.Date == DateOnly.FromDateTime(startTime)
                                       && s.StartTime < endTime.TimeOfDay
                                       && s.EndTime > startTime.TimeOfDay);
        if (overlappingWindow != null && !overlappingWindow.AllowedVisitTypes.Contains(dto.VisitType))
        {
            throw new ArgumentException("Doctor does not offer this visit type during the selected time window");
        }

        var overlapping = await _dbContext.Appointments
            .AnyAsync(a => a.DoctorId == dto.DoctorId
                           && a.StartTime < endTime
                           && a.EndTime > startTime
                           && a.Status != AppointmentStatus.Cancelled
                           && a.Status != AppointmentStatus.Rejected);
        if (overlapping)
        {
            throw new InvalidOperationException("This slot overlaps with an existing appointment");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = dto.DoctorId,
            ClinicId = dto.ClinicId,
            StartTime = startTime,
            EndTime = endTime,
            Status = AppointmentStatus.Pending,
            CreatedAt = _clock.UtcNow,
            VisitType = dto.VisitType,
            PriceCents = priceCents.Value,
            MedicalServiceId = service?.Id,
            ServiceName = service?.Name
        };

        try
        {
            _dbContext.Appointments.Add(appointment);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("This slot was just booked by someone else");
        }

        await _publishEndpoint.Publish(new AppointmentRequested(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartTime,
            appointment.EndTime,
            _clock.UtcNow));

        return new AppointmentInfoDto
        {
            Id = appointment.Id,
            DoctorId = appointment.DoctorId,
            DoctorName = doctor.FullName,
            PatientId = appointment.PatientId,
            ClinicId = appointment.ClinicId,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            Status = appointment.Status,
            VisitType = appointment.VisitType,
            PriceCents = appointment.PriceCents,
            IsPaid = appointment.IsPaid,
            MedicalServiceId = appointment.MedicalServiceId,
            ServiceName = appointment.ServiceName
        };
    }

    public async Task<AppointmentInfoDto> GetByIdAsync(Guid appointmentId, Guid requestingUserId)
    {
        var appointment = await _dbContext.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment == null)
        {
            throw new KeyNotFoundException("Appointment not found");
        }

        var doctor = await _doctorsClient.GetDoctorAsync(appointment.DoctorId);

        var isOwner = appointment.PatientId == requestingUserId
                      || (doctor != null && doctor.UserId == requestingUserId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("You do not have access to this appointment");
        }

        var patient = await _identityClient.GetUserAsync(appointment.PatientId);
        var review = await _dbContext.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);

        return new AppointmentInfoDto
        {
            Id = appointment.Id,
            DoctorId = appointment.DoctorId,
            DoctorName = doctor?.FullName ?? string.Empty,
            PatientId = appointment.PatientId,
            PatientName = patient?.DisplayName ?? string.Empty,
            ClinicId = appointment.ClinicId,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            Status = appointment.Status,
            VisitType = appointment.VisitType,
            PriceCents = appointment.PriceCents,
            IsPaid = appointment.IsPaid,
            MedicalServiceId = appointment.MedicalServiceId,
            ServiceName = appointment.ServiceName,
            HasReview = review != null,
            ReviewRating = review?.Rating,
            ReviewComment = review?.Comment
        };
    }

    public async Task<List<AppointmentInfoDto>> GetForPatientAsync(Guid patientId)
    {
        var queryable = _dbContext.Appointments.Where(a => a.PatientId == patientId);

        var result = await queryable
            .Select(a => new AppointmentInfoDto
            {
                Id = a.Id,
                DoctorId = a.DoctorId,
                PatientId = a.PatientId,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                ClinicId = a.ClinicId,
                Status = a.Status,
                VisitType = a.VisitType,
                PriceCents = a.PriceCents,
                IsPaid = a.IsPaid,
                MedicalServiceId = a.MedicalServiceId,
                ServiceName = a.ServiceName
            })
            .ToListAsync();

        var distinctDoctorIds = result.Select(a => a.DoctorId).Distinct().ToList();
        var doctorNames = new Dictionary<Guid, string>();

        foreach (var doctorId in distinctDoctorIds)
        {
            var doctor = await _doctorsClient.GetDoctorAsync(doctorId);
            if (doctor != null)
            {
                doctorNames[doctorId] = doctor.FullName;
            }
        }

        foreach (var appointment in result)
        {
            if (doctorNames.TryGetValue(appointment.DoctorId, out var name))
            {
                appointment.DoctorName = name;
            }
        }

        var appointmentIds = result.Select(a => a.Id).ToList();
        var reviewsByAppointmentId = await _dbContext.Reviews
            .Where(r => appointmentIds.Contains(r.AppointmentId))
            .ToDictionaryAsync(r => r.AppointmentId);

        foreach (var appointment in result)
        {
            if (reviewsByAppointmentId.TryGetValue(appointment.Id, out var review))
            {
                appointment.HasReview = true;
                appointment.ReviewRating = review.Rating;
                appointment.ReviewComment = review.Comment;
            }
        }

        return result;
    }

    public async Task<List<AppointmentInfoDto>> GetForDoctorByUserIdAsync(Guid userId)
    {
        var doctor = await _doctorsClient.GetDoctorByUserIdAsync(userId);
        if (doctor == null)
        {
            return new List<AppointmentInfoDto>();
        }

        return await GetForDoctorAsync(doctor.Id);
    }

    public async Task<List<AppointmentInfoDto>> GetForDoctorAsync(Guid doctorId)
    {
        var queryable = _dbContext.Appointments.Where(a => a.DoctorId == doctorId);

        var result = await queryable
            .Select(a => new AppointmentInfoDto
            {
                Id = a.Id,
                DoctorId = a.DoctorId,
                PatientId = a.PatientId,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                ClinicId = a.ClinicId,
                Status = a.Status,
                VisitType = a.VisitType,
                PriceCents = a.PriceCents,
                IsPaid = a.IsPaid,
                MedicalServiceId = a.MedicalServiceId,
                ServiceName = a.ServiceName
            })
            .ToListAsync();

        var distinctPatientIds = result.Select(a => a.PatientId).Distinct().ToList();
        var patientNames = new Dictionary<Guid, string>();

        foreach (var patientId in distinctPatientIds)
        {
            var patient = await _identityClient.GetUserAsync(patientId);
            if (patient != null)
            {
                patientNames[patientId] = patient.DisplayName;
            }
        }

        foreach (var appointment in result)
        {
            if (patientNames.TryGetValue(appointment.PatientId, out var name))
            {
                appointment.PatientName = name;
            }
        }

        return result;
    }


    public async Task<bool> ConfirmAsync(Guid appointmentId)
    {
        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment == null)
        {
            throw new ArgumentException("Appointment not found");
        }

        if (appointment.Status == AppointmentStatus.Pending)
        {
            var oldStatus = appointment.Status.ToString();
            appointment.Status = AppointmentStatus.Confirmed;

            await _dbContext.SaveChangesAsync();

            await _publishEndpoint.Publish(new AppointmentConfirmed(
                appointment.Id,
                appointment.PatientId,
                appointment.DoctorId,
                appointment.StartTime,
                appointment.EndTime,
                _clock.UtcNow));
        }
        else
        {
            throw new InvalidOperationException("Can only confirm Pending appointment");
        }

        return true;
    }


    public async Task<bool> RejectAsync(Guid appointmentId)
    {
        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(o => o.Id == appointmentId);
        if (appointment == null)
        {
            throw new ArgumentException("Appointment not found");
        }

        if (appointment.Status == AppointmentStatus.Pending)
        {
            var oldStatus = appointment.Status.ToString();
            appointment.Status = AppointmentStatus.Rejected;

            await _dbContext.SaveChangesAsync();

        }
        else
        {
            throw new InvalidOperationException("Can only pending Appointments be rejected");
        }

        return true;
    }

    public async Task<bool> CancelAsync(Guid appointmentId)
    {
        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(o => o.Id == appointmentId);
        if (appointment == null)
        {
            throw new ArgumentException("Appointment not found");
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            throw new InvalidOperationException("Appointment is already cancelled");
        }

        if (appointment.Status != AppointmentStatus.Pending && appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidOperationException("Only pending or confirmed appointments can be cancelled");
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        await _publishEndpoint.Publish(new AppointmentCancelled(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartTime,
            appointment.EndTime,
            _clock.UtcNow));

        return true;
    }

    public async Task<bool> CompleteAsync(Guid appointmentId)
    {
        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment == null)
        {
            throw new ArgumentException("Appointment not found");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidOperationException("Only confirmed appointments can be completed");
        }

        appointment.Status = AppointmentStatus.Completed;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<ReviewDto> AddReviewAsync(Guid patientId, Guid appointmentId, ReviewCreateDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5");
        }

        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment == null)
        {
            throw new KeyNotFoundException("Appointment not found");
        }

        if (appointment.PatientId != patientId)
        {
            throw new UnauthorizedAccessException("Only the patient of this appointment can review it");
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Only completed appointments can be reviewed");
        }

        var alreadyReviewed = await _dbContext.Reviews.AnyAsync(r => r.AppointmentId == appointmentId);
        if (alreadyReviewed)
        {
            throw new InvalidOperationException("This appointment has already been reviewed");
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            DoctorId = appointment.DoctorId,
            PatientId = patientId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = _clock.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        return new ReviewDto
        {
            Id = review.Id,
            AppointmentId = review.AppointmentId,
            DoctorId = review.DoctorId,
            PatientId = review.PatientId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
    }

    public async Task<DoctorRatingDto> GetDoctorRatingAsync(Guid doctorId)
    {
        var reviews = await _dbContext.Reviews
            .Where(r => r.DoctorId == doctorId)
            .AsNoTracking()
            .ToListAsync();

        return new DoctorRatingDto
        {
            DoctorId = doctorId,
            AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0,
            ReviewCount = reviews.Count
        };
    }

    public async Task<List<DoctorRatingDto>> GetDoctorRatingsAsync(List<Guid> doctorIds)
    {
        var reviews = await _dbContext.Reviews
            .Where(r => doctorIds.Contains(r.DoctorId))
            .AsNoTracking()
            .ToListAsync();

        return doctorIds.Distinct().Select(id =>
        {
            var forDoctor = reviews.Where(r => r.DoctorId == id).ToList();
            return new DoctorRatingDto
            {
                DoctorId = id,
                AverageRating = forDoctor.Count > 0 ? forDoctor.Average(r => r.Rating) : 0,
                ReviewCount = forDoctor.Count
            };
        }).ToList();
    }

    public async Task<List<ReviewDto>> GetDoctorReviewsAsync(Guid doctorId)
    {
        var reviews = await _dbContext.Reviews
            .Where(r => r.DoctorId == doctorId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = reviews.Select(r => new ReviewDto
        {
            Id = r.Id,
            AppointmentId = r.AppointmentId,
            DoctorId = r.DoctorId,
            PatientId = r.PatientId,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();

        var distinctPatientIds = result.Select(r => r.PatientId).Distinct().ToList();
        var patientNames = new Dictionary<Guid, string>();

        foreach (var patientId in distinctPatientIds)
        {
            var patient = await _identityClient.GetUserAsync(patientId);
            if (patient != null)
            {
                patientNames[patientId] = patient.DisplayName;
            }
        }

        foreach (var review in result)
        {
            if (patientNames.TryGetValue(review.PatientId, out var name))
            {
                review.PatientName = name;
            }
        }

        return result;
    }

    public async Task<AdminStatsDto> GetAdminStatsAsync()
    {
        var statusCounts = await _dbContext.Appointments
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountFor(AppointmentStatus status) => statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        var reviews = await _dbContext.Reviews.AsNoTracking().ToListAsync();

        return new AdminStatsDto
        {
            TotalAppointments = statusCounts.Sum(s => s.Count),
            PendingCount = CountFor(AppointmentStatus.Pending),
            ConfirmedCount = CountFor(AppointmentStatus.Confirmed),
            CompletedCount = CountFor(AppointmentStatus.Completed),
            CancelledCount = CountFor(AppointmentStatus.Cancelled),
            RejectedCount = CountFor(AppointmentStatus.Rejected),
            TotalReviews = reviews.Count,
            AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0
        };
    }

    public async Task<List<ReviewDto>> GetAllReviewsAsync()
    {
        var reviews = await _dbContext.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = reviews.Select(r => new ReviewDto
        {
            Id = r.Id,
            AppointmentId = r.AppointmentId,
            DoctorId = r.DoctorId,
            PatientId = r.PatientId,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();

        var distinctPatientIds = result.Select(r => r.PatientId).Distinct().ToList();
        foreach (var patientId in distinctPatientIds)
        {
            var patient = await _identityClient.GetUserAsync(patientId);
            if (patient == null) continue;
            foreach (var review in result.Where(r => r.PatientId == patientId))
            {
                review.PatientName = patient.DisplayName;
            }
        }

        var distinctDoctorIds = result.Select(r => r.DoctorId).Distinct().ToList();
        foreach (var doctorId in distinctDoctorIds)
        {
            var doctor = await _doctorsClient.GetDoctorAsync(doctorId);
            if (doctor == null) continue;
            foreach (var review in result.Where(r => r.DoctorId == doctorId))
            {
                review.DoctorName = doctor.FullName;
            }
        }

        return result;
    }

    public async Task DeleteReviewAsync(Guid reviewId)
    {
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
        if (review == null)
        {
            throw new KeyNotFoundException("Review not found");
        }

        _dbContext.Reviews.Remove(review);
        await _dbContext.SaveChangesAsync();
    }
}
