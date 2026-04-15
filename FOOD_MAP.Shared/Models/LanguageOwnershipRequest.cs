namespace FOOD_MAP.Shared.Models;

public class LanguageOwnershipRequest
{
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public int LanguageId { get; set; }

    public LanguageOwnershipRequestStatus Status { get; set; } = LanguageOwnershipRequestStatus.Pending;

    public DateTimeOffset RequestedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedUtc { get; set; }

    public int? ReviewedByAdminUserId { get; set; }

    public string? RejectionReason { get; set; }

    public User? OwnerUser { get; set; }

    public Language? Language { get; set; }

    public User? ReviewedByAdminUser { get; set; }
}
