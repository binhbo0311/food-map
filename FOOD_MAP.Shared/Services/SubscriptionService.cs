using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Shared.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public SubscriptionService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Subscription?> GetCurrentSubscriptionAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await QueryCurrentSubscription(dbContext, userId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SubscriptionTier> GetCurrentTierAsync(int? userId, CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue)
        {
            return SubscriptionTier.Free;
        }

        var subscription = await GetCurrentSubscriptionAsync(userId.Value, cancellationToken);
        return subscription?.Tier ?? SubscriptionTier.Free;
    }

    public async Task<SubscriptionFeaturePolicy> GetCurrentPolicyAsync(int? userId, CancellationToken cancellationToken = default)
    {
        var tier = await GetCurrentTierAsync(userId, cancellationToken);
        return tier switch
        {
            SubscriptionTier.Premium => new SubscriptionFeaturePolicy(
                SubscriptionTier.Premium,
                MaxAccessiblePoiCount: null,
                MaxOwnerPoiCount: null,
                MaxActivationRadiusMeters: 5000),
            SubscriptionTier.Basic => new SubscriptionFeaturePolicy(
                SubscriptionTier.Basic,
                MaxAccessiblePoiCount: 60,
                MaxOwnerPoiCount: 25,
                MaxActivationRadiusMeters: 400),
            _ => new SubscriptionFeaturePolicy(
                SubscriptionTier.Free,
                MaxAccessiblePoiCount: 15,
                MaxOwnerPoiCount: 6,
                MaxActivationRadiusMeters: 150)
        };
    }

    public async Task<IReadOnlyList<Subscription>> GetUserSubscriptionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Subscription> ProcessPaymentAsync(
        int userId,
        SubscriptionTier tier,
        BillingPeriod billingPeriod,
        decimal amount,
        string currency,
        string paymentProvider,
        string paymentTransactionCode,
        CancellationToken cancellationToken = default)
    {
        if (tier == SubscriptionTier.Free)
        {
            throw new InvalidOperationException("Payment tier must be Basic or Premium.");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        var normalizedCurrencyInput = TextInputNormalizer.NormalizeSingleLine(currency);
        var normalizedCurrency = string.IsNullOrWhiteSpace(normalizedCurrencyInput)
            ? "VND"
            : normalizedCurrencyInput.ToUpperInvariant();

        var normalizedProviderInput = TextInputNormalizer.NormalizeSingleLine(paymentProvider);
        var normalizedProvider = string.IsNullOrWhiteSpace(normalizedProviderInput)
            ? "Manual"
            : normalizedProviderInput;

        var normalizedTransactionCode = TextInputNormalizer.NormalizeSingleLine(paymentTransactionCode);
        if (string.IsNullOrWhiteSpace(normalizedTransactionCode))
        {
            throw new InvalidOperationException("Payment transaction code is required.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException("User account was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var expiresUtc = CalculateExpiresUtc(now, billingPeriod);

        var subscription = new Subscription
        {
            UserId = userId,
            Tier = tier,
            BillingPeriod = billingPeriod,
            PaymentStatus = PaymentStatus.Paid,
            Amount = amount,
            Currency = normalizedCurrency,
            PaymentProvider = normalizedProvider,
            PaymentTransactionCode = normalizedTransactionCode,
            PaidUtc = now,
            StartedUtc = now,
            ExpiresUtc = expiresUtc,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private static IQueryable<Subscription> QueryCurrentSubscription(AppDbContext dbContext, int userId)
    {
        var now = DateTimeOffset.UtcNow;

        return dbContext.Subscriptions
            .Where(x => x.UserId == userId && x.PaymentStatus == PaymentStatus.Paid && x.ExpiresUtc > now)
            .OrderByDescending(x => x.ExpiresUtc)
            .ThenByDescending(x => x.Id);
    }

    private static DateTimeOffset CalculateExpiresUtc(DateTimeOffset startedUtc, BillingPeriod billingPeriod)
    {
        return billingPeriod switch
        {
            BillingPeriod.Yearly => startedUtc.AddYears(1),
            BillingPeriod.Quarterly => startedUtc.AddMonths(3),
            _ => startedUtc.AddMonths(1)
        };
    }
}