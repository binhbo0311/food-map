namespace FOOD_MAP.Shared.Models;

public class OwnerRegistrationRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string BusinessAddress { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public OwnerRegistrationStatus Status { get; set; } = OwnerRegistrationStatus.Pending;

    public DateTimeOffset RequestedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedUtc { get; set; }

    public int? ReviewedByAdminUserId { get; set; }

    public string? RejectionReason { get; set; }

    // Mã owner được cấp khi đơn đăng ký được duyệt.
    public string? ApprovedOwnerCode { get; set; }

    public User? User { get; set; }

    public User? ReviewedByAdminUser { get; set; }
}