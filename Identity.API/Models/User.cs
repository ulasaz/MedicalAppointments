using System.ComponentModel.DataAnnotations;
using Finbuckle.MultiTenant.Abstractions;

namespace Identity.Models;

[MultiTenant]
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
}