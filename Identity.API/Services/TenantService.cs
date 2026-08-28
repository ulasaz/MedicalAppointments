using Identity.Database;
using Identity.DTO_s;
using Identity.Interfaces;
using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Services;

public class TenantService : ITenantService
{
    // Curated on purpose: FontFamily/ButtonRadius drive which CSS the frontend has already
    // preloaded (see TenantService in cura-slot) — an arbitrary value would either be a
    // silent no-op or (for a font) require loading an unvetted remote stylesheet.
    private static readonly HashSet<string> AllowedFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inter", "Poppins", "Merriweather", "Roboto Slab", "Montserrat", "Playfair Display"
    };

    private static readonly HashSet<string> AllowedButtonRadii = new(StringComparer.OrdinalIgnoreCase)
    {
        "pill", "rounded", "sharp"
    };

    private const long MaxBannerBytes = 5 * 1024 * 1024;

    private readonly DatabaseContext _dbContext;
    private readonly IPasswordHelper _passwordHelper;

    public TenantService(DatabaseContext dbContext, IPasswordHelper passwordHelper)
    {
        _dbContext = dbContext;
        _passwordHelper = passwordHelper;
    }

    private static MedicalCenterDto ToDto(MedicalCenter m) => new(
        m.Id, m.Name, m.Slug, m.PrimaryColorHex, m.IsActive, m.CreatedAt,
        m.FontFamily, m.ButtonRadius, m.BannerVideoUrl, m.HasBannerImage);

    public async Task<List<MedicalCenterDto>> GetAllAsync()
    {
        var centers = await _dbContext.MedicalCenters.OrderBy(m => m.Name).ToListAsync();
        return centers.Select(ToDto).ToList();
    }

    public async Task<MedicalCenterDto> GetByIdAsync(Guid id)
    {
        var center = await _dbContext.MedicalCenters.FindAsync(id);
        if (center == null)
        {
            throw new KeyNotFoundException("Medical center not found");
        }

        return ToDto(center);
    }

    public async Task<MedicalCenterDto> CreateAsync(CreateMedicalCenterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(request.Slug))
            throw new ArgumentException("Slug is required");
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            throw new ArgumentException("Admin email is required");
        if (string.IsNullOrWhiteSpace(request.AdminPassword))
            throw new ArgumentException("Admin password is required");
        if (string.IsNullOrWhiteSpace(request.AdminDisplayName))
            throw new ArgumentException("Admin display name is required");

        var slugTaken = await _dbContext.MedicalCenters.AnyAsync(m => m.Slug == request.Slug);
        if (slugTaken)
        {
            throw new InvalidOperationException("A medical center with this slug already exists");
        }

        var emailTaken = await _dbContext.Users.AnyAsync(u => u.Email == request.AdminEmail);
        if (emailTaken)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var center = new MedicalCenter
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            PrimaryColorHex = string.IsNullOrWhiteSpace(request.PrimaryColorHex) ? "#f43f5e" : request.PrimaryColorHex,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.MedicalCenters.Add(center);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = request.AdminEmail,
            DisplayName = request.AdminDisplayName,
            Role = "Admin",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            TenantId = center.Id
        };
        admin.PasswordHash = _passwordHelper.HashPassword(admin, request.AdminPassword);
        _dbContext.Users.Add(admin);

        await _dbContext.SaveChangesAsync();

        return ToDto(center);
    }

    public async Task<MedicalCenterDto> UpdateAsync(Guid requestingAdminId, Guid tenantId, UpdateMedicalCenterRequest request)
    {
        var center = await AuthorizeAndGetCenterAsync(requestingAdminId, tenantId);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(request.PrimaryColorHex))
            throw new ArgumentException("Primary color is required");
        if (!AllowedFonts.Contains(request.FontFamily))
            throw new ArgumentException("Unsupported font");
        if (!AllowedButtonRadii.Contains(request.ButtonRadius))
            throw new ArgumentException("Unsupported button radius");

        center.Name = request.Name;
        center.PrimaryColorHex = request.PrimaryColorHex;
        center.FontFamily = request.FontFamily;
        center.ButtonRadius = request.ButtonRadius;
        center.BannerVideoUrl = string.IsNullOrWhiteSpace(request.BannerVideoUrl) ? null : request.BannerVideoUrl;
        await _dbContext.SaveChangesAsync();

        return ToDto(center);
    }

    public async Task<(byte[] Data, string ContentType)> GetBannerAsync(Guid tenantId)
    {
        var center = await _dbContext.MedicalCenters.FindAsync(tenantId);
        if (center?.BannerImageData == null || center.BannerImageContentType == null)
        {
            throw new KeyNotFoundException("Banner image not found");
        }

        return (center.BannerImageData, center.BannerImageContentType);
    }

    public async Task UploadBannerAsync(Guid requestingAdminId, Guid tenantId, byte[] data, string contentType)
    {
        if (data.Length == 0)
            throw new ArgumentException("No image was uploaded");
        if (data.Length > MaxBannerBytes)
            throw new ArgumentException("Banner image must be 5MB or smaller");

        var center = await AuthorizeAndGetCenterAsync(requestingAdminId, tenantId);
        center.BannerImageData = data;
        center.BannerImageContentType = contentType;
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteBannerAsync(Guid requestingAdminId, Guid tenantId)
    {
        var center = await AuthorizeAndGetCenterAsync(requestingAdminId, tenantId);
        center.BannerImageData = null;
        center.BannerImageContentType = null;
        await _dbContext.SaveChangesAsync();
    }

    private async Task<MedicalCenter> AuthorizeAndGetCenterAsync(Guid requestingAdminId, Guid tenantId)
    {
        var requestingAdmin = await _dbContext.Users.FindAsync(requestingAdminId);
        if (requestingAdmin == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // A center-scoped admin may only rebrand their own center; the platform
        // super-admin (TenantId == null) may edit any of them.
        if (requestingAdmin.TenantId.HasValue && requestingAdmin.TenantId.Value != tenantId)
        {
            throw new UnauthorizedAccessException("Cannot modify another medical center");
        }

        var center = await _dbContext.MedicalCenters.FindAsync(tenantId);
        if (center == null)
        {
            throw new KeyNotFoundException("Medical center not found");
        }

        return center;
    }
}
