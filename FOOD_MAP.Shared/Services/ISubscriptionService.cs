using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Shared.Services;

public sealed record SubscriptionFeaturePolicy(
    SubscriptionTier Tier,
    int? MaxAccessiblePoiCount,
    int? MaxOwnerPoiCount,
    int MaxActivationRadiusMeters);

public interface ISubscriptionService
{
    Task<Subscription?> GetCurrentSubscriptionAsync(int userId, CancellationToken cancellationToken = default);

    Task<SubscriptionTier> GetCurrentTierAsync(int? userId, CancellationToken cancellationToken = default);

    Task<SubscriptionFeaturePolicy> GetCurrentPolicyAsync(int? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> GetUserSubscriptionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<Subscription> ProcessPaymentAsync(
        int userId,
        SubscriptionTier tier,
        BillingPeriod billingPeriod,
        decimal amount,
        string currency,
        string paymentProvider,
        string paymentTransactionCode,
        CancellationToken cancellationToken = default);
}