using Doctors.Database;
using Doctors.DTOs;
using Doctors.Interfaces;
using Doctors.Models;
using Microsoft.EntityFrameworkCore;

namespace Doctors.Services;

public class MedicalServiceService : IMedicalServiceService
{
    private readonly DatabaseContext _dbContext;

    public MedicalServiceService(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<MedicalService>> GetForDoctorAsync(Guid doctorProfileId)
    {
        return await _dbContext.MedicalServices
            .AsNoTracking()
            .Where(s => s.DoctorProfileId == doctorProfileId)
            .ToListAsync();
    }

    public async Task<MedicalService> GetByIdAsync(Guid doctorProfileId, Guid serviceId)
    {
        return await GetOwnedServiceAsync(doctorProfileId, serviceId);
    }

    public async Task<MedicalService> CreateAsync(Guid doctorProfileId, Guid requestingUserId, MedicalServiceUpsertDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAllowedVisitTypes(request.AllowedVisitTypes);
        ValidatePrice(request.PriceCents);

        await EnsureOwnerAsync(doctorProfileId, requestingUserId);

        var service = new MedicalService
        {
            Id = Guid.NewGuid(),
            DoctorProfileId = doctorProfileId,
            Name = request.Name,
            Description = request.Description,
            PriceCents = request.PriceCents,
            AllowedVisitTypes = request.AllowedVisitTypes.Distinct().ToList(),
        };

        _dbContext.MedicalServices.Add(service);
        await _dbContext.SaveChangesAsync();

        return service;
    }

    public async Task<MedicalService> UpdateAsync(Guid doctorProfileId, Guid serviceId, Guid requestingUserId, MedicalServiceUpsertDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAllowedVisitTypes(request.AllowedVisitTypes);
        ValidatePrice(request.PriceCents);

        await EnsureOwnerAsync(doctorProfileId, requestingUserId);

        var service = await GetOwnedServiceAsync(doctorProfileId, serviceId);

        service.Name = request.Name;
        service.Description = request.Description;
        service.PriceCents = request.PriceCents;
        service.AllowedVisitTypes = request.AllowedVisitTypes.Distinct().ToList();

        await _dbContext.SaveChangesAsync();

        return service;
    }

    public async Task DeleteAsync(Guid doctorProfileId, Guid serviceId, Guid requestingUserId)
    {
        await EnsureOwnerAsync(doctorProfileId, requestingUserId);

        var service = await GetOwnedServiceAsync(doctorProfileId, serviceId);

        _dbContext.MedicalServices.Remove(service);
        await _dbContext.SaveChangesAsync();
    }

    private async Task EnsureOwnerAsync(Guid doctorProfileId, Guid requestingUserId)
    {
        var doctor = await _dbContext.DoctorProfiles.FindAsync(doctorProfileId);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor profile does not exist");
        }

        if (doctor.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the owning doctor can manage this profile's services");
        }
    }

    private async Task<MedicalService> GetOwnedServiceAsync(Guid doctorProfileId, Guid serviceId)
    {
        var service = await _dbContext.MedicalServices
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.DoctorProfileId == doctorProfileId);

        if (service == null)
        {
            throw new KeyNotFoundException("Medical service does not exist");
        }

        return service;
    }

    private static void ValidateAllowedVisitTypes(List<VisitType> allowedVisitTypes)
    {
        if (allowedVisitTypes == null || allowedVisitTypes.Count == 0)
        {
            throw new ArgumentException("At least one visit type (online or stationary) must be selected.");
        }
    }

    private static void ValidatePrice(int priceCents)
    {
        if (priceCents < 0)
        {
            throw new ArgumentException("Price cannot be negative.");
        }
    }
}
