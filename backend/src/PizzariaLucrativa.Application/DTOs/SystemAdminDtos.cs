namespace PizzariaLucrativa.Application.DTOs;

public sealed record TenantListItemDto(
    Guid Id,
    string Name,
    string? Slug,
    DateTime CreatedAt,
    string OwnerName,
    string OwnerEmail,
    bool IsActive,
    DateTime? DeactivatedAt,
    string? DeactivationReason
);

public sealed record TenantStatusUpdateInput(bool IsActive, string? Reason);

public sealed record SystemAdminLoginInput(string Email, string Password);

public sealed record SystemAdminAuthResultDto(string Token, DateTime ExpiresAt, string Email, string FullName);

public sealed record ForgotPasswordInput(string Email);

public sealed record ResetPasswordInput(string Token, string NewPassword);
