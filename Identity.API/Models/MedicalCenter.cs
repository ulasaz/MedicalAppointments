using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Identity.Models;

public class MedicalCenter
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    /// <summary>Immutable after creation. Human-friendly identifier, not currently used for
    /// routing — tenant resolution elsewhere uses this center's <see cref="Id"/> directly
    /// (as a JWT claim or the X-Tenant-Id header), kept here for display/uniqueness.</summary>
    public string Slug { get; set; }
    public string PrimaryColorHex { get; set; } = "#f43f5e";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    /// <summary>One of a curated set the frontend preloads (see TenantService) — not
    /// free-text, so we never need to load an arbitrary/unpreloaded web font at runtime.</summary>
    public string FontFamily { get; set; } = "Inter";

    /// <summary>Controls corner rounding on brand-colored buttons app-wide: "pill" | "rounded" | "sharp".</summary>
    public string ButtonRadius { get; set; } = "pill";

    /// <summary>Optional hosted video (e.g. an S3/CDN mp4 URL) shown instead of the default
    /// animated hero background. Takes priority over <see cref="HasBannerImage"/> when set.</summary>
    public string? BannerVideoUrl { get; set; }

    [JsonIgnore]
    public byte[]? BannerImageData { get; set; }

    [JsonIgnore]
    public string? BannerImageContentType { get; set; }

    [NotMapped]
    public bool HasBannerImage => BannerImageData != null;
}
