using Doctors.DTOs;
using Doctors.Interfaces;

namespace Doctors.Api.Tests.Infrastructure;

public class FakeAppointmentsClient : IAppointmentsClient
{
    /// <summary>Per-id overrides, for tests that need distinct doctors to resolve to distinct ratings.</summary>
    public Dictionary<Guid, DoctorRatingDto> Ratings { get; } = new();

    public Task<DoctorRatingDto?> GetRatingAsync(Guid doctorId)
    {
        Ratings.TryGetValue(doctorId, out var rating);
        return Task.FromResult(rating);
    }

    public Task<Dictionary<Guid, DoctorRatingDto>> GetRatingsAsync(IEnumerable<Guid> doctorIds)
    {
        var result = doctorIds
            .Where(Ratings.ContainsKey)
            .ToDictionary(id => id, id => Ratings[id]);
        return Task.FromResult(result);
    }
}
