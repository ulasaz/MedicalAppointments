using Appointments.DTOs;
using Appointments.Interfaces;

namespace Appointments.Api.Tests.Infrastructure;

public class FakeDoctorsClient : IDoctorsClient
{
    /// <summary>Returned for any doctorId not present in <see cref="Doctors"/>.</summary>
    public DoctorInfoDto? Doctor { get; set; }

    /// <summary>Per-id overrides, for tests that need distinct doctors to resolve to distinct data.</summary>
    public Dictionary<Guid, DoctorInfoDto> Doctors { get; } = new();

    public Task<DoctorInfoDto?> GetDoctorAsync(Guid doctorId)
    {
        if (Doctors.TryGetValue(doctorId, out var doctor))
        {
            return Task.FromResult<DoctorInfoDto?>(doctor);
        }

        return Task.FromResult(Doctor);
    }

    public Task<DoctorInfoDto?> GetDoctorByUserIdAsync(Guid userId)
    {
        var match = Doctors.Values.FirstOrDefault(d => d.UserId == userId)
                    ?? (Doctor?.UserId == userId ? Doctor : null);
        return Task.FromResult(match);
    }

    /// <summary>Returned for any serviceId not present in <see cref="Services"/>.</summary>
    public MedicalServiceInfoDto? Service { get; set; }

    /// <summary>Per-id overrides, for tests that need distinct services to resolve to distinct data.</summary>
    public Dictionary<Guid, MedicalServiceInfoDto> Services { get; } = new();

    public Task<MedicalServiceInfoDto?> GetServiceAsync(Guid doctorId, Guid serviceId)
    {
        if (Services.TryGetValue(serviceId, out var service))
        {
            return Task.FromResult<MedicalServiceInfoDto?>(service);
        }

        return Task.FromResult(Service);
    }
}
