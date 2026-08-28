using Identity.DTO_s;

namespace Identity.Interfaces;

public interface ITenantService
{
    Task<List<MedicalCenterDto>> GetAllAsync();
    Task<MedicalCenterDto> GetByIdAsync(Guid id);
    Task<MedicalCenterDto> CreateAsync(CreateMedicalCenterRequest request);
    Task<MedicalCenterDto> UpdateAsync(Guid requestingAdminId, Guid tenantId, UpdateMedicalCenterRequest request);
    Task<(byte[] Data, string ContentType)> GetBannerAsync(Guid tenantId);
    Task UploadBannerAsync(Guid requestingAdminId, Guid tenantId, byte[] data, string contentType);
    Task DeleteBannerAsync(Guid requestingAdminId, Guid tenantId);
}
