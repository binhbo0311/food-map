using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Shared.Services;

public sealed record SubscriptionPlanWriteModel(
    string PlanCode,
    string DisplayName,
    SubscriptionTier Tier,
    BillingPeriod BillingPeriod,
    decimal FixedPrice,
    string Currency,
    int? MaxAccessiblePoiCount,
    int? MaxOwnerPoiCount,
    int MaxActivationRadiusMeters,
    bool IsActive);

public interface ISubscriptionPlanService
{
    Task<IReadOnlyList<SubscriptionPlan>> GetVisiblePlansAsync(int actorUserId, CancellationToken cancellationToken = default);

    Task<SubscriptionPlan> CreatePlanAsync(int adminUserId, SubscriptionPlanWriteModel model, CancellationToken cancellationToken = default);

    Task<SubscriptionPlan> UpdatePlanAsync(int adminUserId, int planId, SubscriptionPlanWriteModel model, CancellationToken cancellationToken = default);
}