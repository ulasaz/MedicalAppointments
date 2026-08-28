using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Finbuckle.MultiTenant.Abstractions;

namespace Doctors.Models;

[MultiTenant]
public class DoctorProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; }
    public string Specialization { get; set; }
    public string City { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int? PriceStationaryCents { get; set; }
    public int? PriceOnlineCents { get; set; }
    public List<string> ConditionsTreated { get; set; } = new();

    /// <summary>Raw image bytes — persisted, but never serialized into the normal profile JSON
    /// (that would bloat every search result); fetched separately via GET /doctors/{id}/photo.</summary>
    [JsonIgnore]
    public byte[]? PhotoData { get; set; }

    [JsonIgnore]
    public string? PhotoContentType { get; set; }

    [NotMapped]
    public bool HasPhoto => PhotoData != null;

    /// <summary>Resolved live from Appointments.API's reviews; not persisted here.</summary>
    [NotMapped]
    public double? AverageRating { get; set; }

    [NotMapped]
    public int ReviewCount { get; set; }
}
