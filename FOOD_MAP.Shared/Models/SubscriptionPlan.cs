namespace FOOD_MAP.Shared.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    public decimal FixedPrice { get; set; }

    public string Currency { get; set; } = "VND";

    public int? MaxAccessiblePoiCount { get; set; }

    public int? MaxOwnerPoiCount { get; set; }

    public int MaxActivationRadiusMeters { get; set; } = 150;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public int CreatedByAdminUserId { get; set; }

    public int? UpdatedByAdminUserId { get; set; }

    public User? CreatedByAdminUser { get; set; }

    public User? UpdatedByAdminUser { get; set; }
}