using Doctors.Database;
using Doctors.DTOs;
using Doctors.Interfaces;
using Doctors.Models;
using Microsoft.EntityFrameworkCore;

namespace Doctors.Services;

public class DoctorService : IDoctorService
{
    private readonly DatabaseContext _dbContext;
    private readonly IAppointmentsClient _appointmentsClient;

    public DoctorService(DatabaseContext dbContext, IAppointmentsClient appointmentsClient)
    {
        _dbContext = dbContext;
        _appointmentsClient = appointmentsClient;
    }

    public async Task<IEnumerable<DoctorProfile>> SearchAsync(string? specialization, string? city, string? name)
    {
        var query = _dbContext.DoctorProfiles.AsNoTracking().Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(specialization))
        {
            query = query.Where(d => EF.Functions.ILike(d.Specialization, $"%{specialization}%"));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(d => EF.Functions.ILike(d.City, $"%{city}%"));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(d => EF.Functions.ILike(d.FullName, $"%{name}%"));
        }

        var doctors = await query.ToListAsync();

        var ratings = await _appointmentsClient.GetRatingsAsync(doctors.Select(d => d.Id));
        foreach (var doctor in doctors)
        {
            if (ratings.TryGetValue(doctor.Id, out var rating) && rating.ReviewCount > 0)
            {
                doctor.AverageRating = rating.AverageRating;
                doctor.ReviewCount = rating.ReviewCount;
            }
        }

        return doctors;
    }

    public async Task<DoctorProfile> GetByIdAsync(Guid id)
    {
        var doctor = await _dbContext.DoctorProfiles.FindAsync(id);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        var rating = await _appointmentsClient.GetRatingAsync(id);
        if (rating != null && rating.ReviewCount > 0)
        {
            doctor.AverageRating = rating.AverageRating;
            doctor.ReviewCount = rating.ReviewCount;
        }

        return doctor;
    }

    public async Task<DoctorProfile> GetByUserIdAsync(Guid userId)
    {
        var doctor = await _dbContext.DoctorProfiles.FirstOrDefaultAsync(d => d.UserId == userId);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        return doctor;
    }

    public async Task<DoctorProfile> CreateAsync(Guid userId, DoctorProfileUpdateDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var alreadyExists = await _dbContext.DoctorProfiles.AnyAsync(d => d.UserId == userId);

        if (alreadyExists)
        {
            throw new InvalidOperationException("A doctor profile already exists for this user");
        }

        var doctor = new DoctorProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = profile.FullName,
            Specialization = profile.Specialization,
            City = profile.City,
            Description = profile.Description,
            IsActive = profile.IsActive,
            PriceStationaryCents = profile.PriceStationaryCents,
            PriceOnlineCents = profile.PriceOnlineCents,
            ConditionsTreated = profile.ConditionsTreated ?? new(),
        };

        _dbContext.DoctorProfiles.Add(doctor);
        await _dbContext.SaveChangesAsync();

        return doctor;
    }

    public async Task<DoctorProfile> UpdateAsync(Guid id, Guid requestingUserId, DoctorProfileUpdateDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var doctor = await _dbContext.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can edit this profile");
        }

        doctor.FullName = profile.FullName;
        doctor.Specialization = profile.Specialization;
        doctor.City = profile.City;
        doctor.Description = profile.Description;
        doctor.IsActive = profile.IsActive;
        doctor.PriceStationaryCents = profile.PriceStationaryCents;
        doctor.PriceOnlineCents = profile.PriceOnlineCents;
        doctor.ConditionsTreated = profile.ConditionsTreated ?? new();

        await _dbContext.SaveChangesAsync();

        var rating = await _appointmentsClient.GetRatingAsync(id);
        if (rating != null && rating.ReviewCount > 0)
        {
            doctor.AverageRating = rating.AverageRating;
            doctor.ReviewCount = rating.ReviewCount;
        }

        return doctor;
    }

    private static readonly HashSet<string> AllowedPhotoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png"
    };

    public async Task<DoctorPhoto> GetPhotoAsync(Guid id)
    {
        var doctor = await _dbContext.DoctorProfiles.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.PhotoData, d.PhotoContentType })
            .FirstOrDefaultAsync();

        if (doctor?.PhotoData == null || doctor.PhotoContentType == null)
        {
            throw new KeyNotFoundException("This doctor has no profile photo");
        }

        return new DoctorPhoto(doctor.PhotoData, doctor.PhotoContentType);
    }

    public async Task UploadPhotoAsync(Guid id, Guid requestingUserId, byte[] data, string contentType)
    {
        if (!AllowedPhotoContentTypes.Contains(contentType))
        {
            throw new ArgumentException("Photo must be a JPEG or PNG image.");
        }

        var doctor = await _dbContext.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can change this profile's photo");
        }

        doctor.PhotoData = data;
        doctor.PhotoContentType = contentType;

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeletePhotoAsync(Guid id, Guid requestingUserId)
    {
        var doctor = await _dbContext.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can change this profile's photo");
        }

        doctor.PhotoData = null;
        doctor.PhotoContentType = null;

        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<DoctorProfile>> GetAllForAdminAsync()
    {
        // Unlike SearchAsync (patient-facing), this intentionally includes inactive
        // profiles too — an admin moderating the directory needs to see and be able
        // to re-activate ones that are currently hidden from search.
        return await _dbContext.DoctorProfiles
            .AsNoTracking()
            .OrderBy(d => d.FullName)
            .ToListAsync();
    }

    public async Task<DoctorProfile> SetActiveStatusAsync(Guid id, bool isActive)
    {
        var doctor = await _dbContext.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        doctor.IsActive = isActive;
        await _dbContext.SaveChangesAsync();

        return doctor;
    }
}
