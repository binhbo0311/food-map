using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Shared.Services;

public sealed class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public SubscriptionPlanService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetVisiblePlansAsync(int actorUserId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var actorRole = await GetActorRoleAsync(dbContext, actorUserId, cancellationToken);

        if (actorRole is not UserRole.Owner and not UserRole.Admin)
        {
            throw new InvalidOperationException("Only owner or admin accounts can view subscription plans.");
        }

        return await dbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Tier)
            .ThenBy(x => x.BillingPeriod)
            .ThenBy(x => x.FixedPrice)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPlan> CreatePlanAsync(int adminUserId, SubscriptionPlanWriteModel model, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminRoleAsync(dbContext, adminUserId, cancellationToken);

        var normalizedPlanCode = NormalizePlanCode(model.PlanCode);
        var normalizedDisplayName = NormalizeDisplayName(model.DisplayName);
        var normalizedCurrency = NormalizeCurrency(model.Currency);

        var isDuplicatePlanCode = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.PlanCode.ToLower() == normalizedPlanCode.ToLower(), cancellationToken);

        if (isDuplicatePlanCode)
        {
            throw new InvalidOperationException("Plan code already exists.");
        }

        var isDuplicateTierAndPeriod = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.Tier == model.Tier && x.BillingPeriod == model.BillingPeriod, cancellationToken);

        if (isDuplicateTierAndPeriod)
        {
            throw new InvalidOperationException("Tier and billing period already have a configured plan.");
        }

        ValidateWriteModel(model);

        var now = DateTimeOffset.UtcNow;
        var plan = new SubscriptionPlan
        {
            PlanCode = normalizedPlanCode,
            DisplayName = normalizedDisplayName,
            Tier = model.Tier,
            BillingPeriod = model.BillingPeriod,
            FixedPrice = model.FixedPrice,
            Currency = normalizedCurrency,
            MaxAccessiblePoiCount = model.MaxAccessiblePoiCount,
            MaxOwnerPoiCount = model.MaxOwnerPoiCount,
            MaxActivationRadiusMeters = model.MaxActivationRadiusMeters,
            IsActive = model.IsActive,
            CreatedUtc = now,
            UpdatedUtc = now,
            CreatedByAdminUserId = adminUserId,
            UpdatedByAdminUserId = null
        };

        dbContext.SubscriptionPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<SubscriptionPlan> UpdatePlanAsync(int adminUserId, int planId, SubscriptionPlanWriteModel model, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminRoleAsync(dbContext, adminUserId, cancellationToken);

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(x => x.Id == planId, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException("Subscription plan was not found.");
        }

        ValidateWriteModel(model);

        var normalizedPlanCode = NormalizePlanCode(model.PlanCode);
        var normalizedDisplayName = NormalizeDisplayName(model.DisplayName);
        var normalizedCurrency = NormalizeCurrency(model.Currency);

        var isDuplicatePlanCode = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.Id != planId && x.PlanCode.ToLower() == normalizedPlanCode.ToLower(), cancellationToken);

        if (isDuplicatePlanCode)
        {
            throw new InvalidOperationException("Plan code already exists.");
        }

        var isDuplicateTierAndPeriod = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.Id != planId && x.Tier == model.Tier && x.BillingPeriod == model.BillingPeriod, cancellationToken);

        if (isDuplicateTierAndPeriod)
        {
            throw new InvalidOperationException("Tier and billing period already have a configured plan.");
        }

        plan.PlanCode = normalizedPlanCode;
        plan.DisplayName = normalizedDisplayName;
        plan.Tier = model.Tier;
        plan.BillingPeriod = model.BillingPeriod;
        plan.FixedPrice = model.FixedPrice;
        plan.Currency = normalizedCurrency;
        plan.MaxAccessiblePoiCount = model.MaxAccessiblePoiCount;
        plan.MaxOwnerPoiCount = model.MaxOwnerPoiCount;
        plan.MaxActivationRadiusMeters = model.MaxActivationRadiusMeters;
        plan.IsActive = model.IsActive;
        plan.UpdatedUtc = DateTimeOffset.UtcNow;
        plan.UpdatedByAdminUserId = adminUserId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return plan;
    }

    private static void ValidateWriteModel(SubscriptionPlanWriteModel model)
    {
        if (model.FixedPrice < 0)
        {
            throw new InvalidOperationException("Fixed price must be zero or greater.");
        }

        if (model.MaxActivationRadiusMeters <= 0)
        {
            throw new InvalidOperationException("Max activation radius must be greater than zero.");
        }

        if (model.MaxAccessiblePoiCount.HasValue && model.MaxAccessiblePoiCount.Value <= 0)
        {
            throw new InvalidOperationException("Max accessible POI count must be greater than zero.");
        }

        if (model.MaxOwnerPoiCount.HasValue && model.MaxOwnerPoiCount.Value <= 0)
        {
            throw new InvalidOperationException("Max owner POI count must be greater than zero.");
        }
    }

    private static async Task<UserRole> GetActorRoleAsync(AppDbContext dbContext, int actorUserId, CancellationToken cancellationToken)
    {
        var role = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == actorUserId)
            .Select(x => (UserRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (!role.HasValue)
        {
            throw new InvalidOperationException("User account was not found.");
        }

        return role.Value;
    }

    private static async Task EnsureAdminRoleAsync(AppDbContext dbContext, int adminUserId, CancellationToken cancellationToken)
    {
        var role = await GetActorRoleAsync(dbContext, adminUserId, cancellationToken);
        if (role != UserRole.Admin)
        {
            throw new InvalidOperationException("Only admin accounts can modify subscription plans.");
        }
    }

    private static string NormalizePlanCode(string rawValue)
    {
        var normalized = TextInputNormalizer.NormalizeSingleLine(rawValue).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Plan code is required.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string rawValue)
    {
        var normalized = TextInputNormalizer.NormalizeSingleLine(rawValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Display name is required.");
        }

        return normalized;
    }

    private static string NormalizeCurrency(string rawValue)
    {
        var normalized = TextInputNormalizer.NormalizeSingleLine(rawValue).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "VND" : normalized;
    }
}