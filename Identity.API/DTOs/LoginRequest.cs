namespace Identity.DTO_s;

public record LoginRequest(
    string Email, 
    string Password
);