namespace Shared;

public record OrderStatusChanged(
    Guid OrderId,
    Guid CustomerId,
    string OldStatus,
    string NewStatus,
    string? Reason,        // populated on Rejected / Cancelled
    DateTimeOffset ChangedAt
);
