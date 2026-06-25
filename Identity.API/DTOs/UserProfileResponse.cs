namespace Identity.DTO_s;

public record UserProfileResponse
(
    Guid Id,
    string Email,
    string DisplayName,
    string Role
);