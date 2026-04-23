namespace FOOD_MAP.Shared.Models;

public class PaymentTransaction
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int? SubscriptionId { get; set; }

    public PaymentProviderType PaymentProvider { get; set; } = PaymentProviderType.Manual;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public string TransactionCode { get; set; } = string.Empty;

    public string? ProviderReferenceCode { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public string? Notes { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedUtc { get; set; }

    public User? User { get; set; }

    public Subscription? Subscription { get; set; }
}