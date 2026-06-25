namespace Identity.DTO_s;

public record RegisterRequest(
    string Email, 
    string Password, 
    string DisplayName
);