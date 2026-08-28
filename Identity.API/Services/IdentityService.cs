using Identity.Database;
using Identity.DTO_s;
using Identity.Interfaces;
using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly ITokenService _tokenService;
    private readonly DatabaseContext _dbContext;
    private readonly IPasswordHelper  _passwordHelper;

    public IdentityService(ITokenService tokenService, DatabaseContext dbContext, IPasswordHelper passwordHelper)
    {
        _tokenService = tokenService;
        _dbContext = dbContext;
        _passwordHelper = passwordHelper;
    }

    private static readonly HashSet<string> AllowedRegistrationRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Patient", "Doctor"
    };

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ArgumentException("Name is required");
        if (!AllowedRegistrationRoles.Contains(request.Role ?? string.Empty))
            throw new ArgumentException("Role must be Patient or Doctor");

        var center = await _dbContext.MedicalCenters.FirstOrDefaultAsync(m => m.Id == request.TenantId && m.IsActive);
        if (center == null)
        {
            throw new ArgumentException("Medical center not found or not accepting new accounts");
        }

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            DisplayName = request.DisplayName,
            Id = Guid.NewGuid(),
            Role = request.Role,
            TenantId = request.TenantId
        };
        
        user.PasswordHash = _passwordHelper.HashPassword(user, request.Password);
        
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        var token = _tokenService.GenerateToken(user);
        return new AuthResponse(token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) 
            throw new ArgumentException("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password)) 
            throw new ArgumentException("Password is required");
        
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (existingUser == null || !_passwordHelper.VerifyPassword(existingUser, existingUser.PasswordHash, request.Password))
        {
            throw new InvalidOperationException("Invalid email or password");
        }

        if (!existingUser.IsActive)
        {
            throw new UnauthorizedAccessException("Account is deactivated");
        }

        var token = _tokenService.GenerateToken(existingUser);
        return new AuthResponse(token);
        
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
    
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return new UserProfileResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.CreatedAt,
            user.IsActive,
            user.TenantId
        );
    }

    public async Task<List<UserProfileResponse>> GetAllUsersAsync(Guid requestingAdminId)
    {
        var requestingAdmin = await _dbContext.Users.FindAsync(requestingAdminId);
        if (requestingAdmin == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // The platform super-admin (TenantId == null) sees every user across every
        // medical center; a center-scoped admin only ever sees their own center's users.
        var query = _dbContext.Users.AsQueryable();
        if (requestingAdmin.TenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == requestingAdmin.TenantId);
        }

        return await query
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserProfileResponse(u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt, u.IsActive, u.TenantId))
            .ToListAsync();
    }

    public async Task<UserProfileResponse> SetUserActiveStatusAsync(Guid requestingAdminId, Guid targetUserId, bool isActive)
    {
        if (requestingAdminId == targetUserId)
        {
            throw new InvalidOperationException("An admin cannot change their own account status");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        user.IsActive = isActive;
        await _dbContext.SaveChangesAsync();

        return new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt, user.IsActive, user.TenantId);
    }

    public async Task<UserProfileResponse> UpdateDisplayNameAsync(Guid userId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Name is required");

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        user.DisplayName = displayName;
        await _dbContext.SaveChangesAsync();

        return new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt, user.IsActive, user.TenantId);
    }
}