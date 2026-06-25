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

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) 
            throw new ArgumentException("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password)) 
            throw new ArgumentException("Password is required");
        if (string.IsNullOrWhiteSpace(request.DisplayName)) 
            throw new ArgumentException("Name is required");
        
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
            Role = "Customer"
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
            user.Role
        );
    }
}