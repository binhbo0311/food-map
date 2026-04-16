namespace FOOD_MAP.Shared.Models;

public class Subscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public string? PaymentProvider { get; set; }

    public string? PaymentTransactionCode { get; set; }

    public DateTimeOffset? PaidUtc { get; set; }

    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}