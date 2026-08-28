using System.ComponentModel.DataAnnotations;

namespace Identity.Models;

public class User
{
    public Guid Id { get; set; }
    
    [Required]
    public string DisplayName { get; set; }
    
    public string PasswordHash { get; set; }
    
    [Required]
    public string Email { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public string Role { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Which medical center this user belongs to. Null only for the platform
    /// super-admin — every Patient, Doctor, and center-scoped Admin has one.</summary>
    public Guid? TenantId { get; set; }
}