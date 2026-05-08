using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Shared.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<POI> Pois => Set<POI>();

    public DbSet<POITranslation> PoiTranslations => Set<POITranslation>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<SyncHistory> SyncHistories => Set<SyncHistory>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();

    public DbSet<UserTour> UserTours => Set<UserTour>();

    public DbSet<TourList> TourLists => Set<TourList>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    public DbSet<OwnerRegistrationRequest> OwnerRegistrationRequests => Set<OwnerRegistrationRequest>();

    public DbSet<LanguageOwnershipRequest> LanguageOwnershipRequests => Set<LanguageOwnershipRequest>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<ActiveClientHeartbeat> ActiveClientHeartbeats => Set<ActiveClientHeartbeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("Languages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
            entity.Property(x => x.LanguageName).IsRequired().HasMaxLength(100);
            entity.HasIndex(x => x.LanguageCode).IsUnique();
        });

        modelBuilder.Entity<POI>(entity =>
        {
            entity.ToTable("POIs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(20)
                .ValueGeneratedNever();

            entity.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbPoiType(x), x => FromDbPoiType(x));

            entity.Property(x => x.Latitude).IsRequired();
            entity.Property(x => x.Longitude).IsRequired();
            entity.Property(x => x.ActivationRadius).IsRequired();
            entity.Property(x => x.Priority).IsRequired();

            entity.Property(x => x.ApprovalStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbPoiApprovalStatus(x), x => FromDbPoiApprovalStatus(x));

            entity.Property(x => x.SubmittedUtc).IsRequired();
            entity.Property(x => x.ReviewedUtc);
            entity.Property(x => x.OwnerId);
            entity.Property(x => x.ReviewedByAdminUserId);
            entity.Property(x => x.QRCodeId).HasMaxLength(100);

            // Đếm tổng số lần TTS phát cho POI này; mặc định = 0, chỉ tăng (không bao giờ giảm).
            entity.Property(x => x.ListenCount).IsRequired().HasDefaultValue(0);

            entity.HasOne(x => x.Owner)
                .WithMany(x => x.OwnedPois)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ReviewedByAdminUser)
                .WithMany(x => x.ReviewedPois)
                .HasForeignKey(x => x.ReviewedByAdminUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.Type, x.ApprovalStatus });
            entity.HasIndex(x => x.OwnerId);
        });

        modelBuilder.Entity<POITranslation>(entity =>
        {
            entity.ToTable("POITranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PoiId).IsRequired().HasMaxLength(20);
            entity.Property(x => x.LocationName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
            entity.Property(x => x.AudioFileUrl).IsRequired().HasMaxLength(500);
            entity.Property(x => x.TtsScript).IsRequired();
            entity.Property(x => x.RichContentHtml).IsRequired().HasDefaultValue(string.Empty);

            // Cấu hình khóa ngoại giữa POITranslation và POI.
            entity.HasOne(x => x.Poi)
                .WithMany(x => x.PoiTranslations)
                .HasForeignKey(x => x.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình khóa ngoại giữa POITranslation và Language.
            entity.HasOne(x => x.Language)
                .WithMany(x => x.PoiTranslations)
                .HasForeignKey(x => x.LanguageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique index cho cặp (PoiId, LanguageId) theo yêu cầu Localization pattern.
            entity.HasIndex(x => new { x.PoiId, x.LanguageId }).IsUnique();
        });

        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.ToTable("MediaAssets");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PoiTranslationId).IsRequired();
            entity.Property(x => x.AssetType).IsRequired().HasMaxLength(20);
            entity.Property(x => x.RemoteUrl).IsRequired().HasMaxLength(500);
            entity.Property(x => x.LocalPath).IsRequired().HasMaxLength(500);
            entity.Property(x => x.FileName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.IsDownloaded).IsRequired();
            entity.Property(x => x.LastDownloadedUtc);

            // Mỗi bản ghi cache media phải gắn với một translation cụ thể.
            entity.HasOne(x => x.PoiTranslation)
                .WithMany(x => x.MediaAssets)
                .HasForeignKey(x => x.PoiTranslationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mỗi translation chỉ có tối đa một file cho từng loại media.
            entity.HasIndex(x => new { x.PoiTranslationId, x.AssetType }).IsUnique();
        });

        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.ToTable("SyncHistories");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.SyncType).IsRequired().HasMaxLength(50);
            entity.Property(x => x.StartedUtc).IsRequired();
            entity.Property(x => x.CompletedUtc);
            entity.Property(x => x.IsSuccess).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(150);

            entity.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbUserRole(x), x => FromDbUserRole(x));

            entity.Property(x => x.OwnerIdentificationCode).HasMaxLength(30);
            entity.Property(x => x.OwnerApprovedUtc);
            entity.Property(x => x.CreatedUtc).IsRequired();

            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.OwnerIdentificationCode).IsUnique();
        });

        modelBuilder.Entity<OwnerRegistrationRequest>(entity =>
        {
            entity.ToTable("OwnerRegistrationRequests");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.BusinessName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.BusinessAddress).IsRequired().HasMaxLength(300);
            entity.Property(x => x.ContactPhone).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(1000);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbOwnerRegistrationStatus(x), x => FromDbOwnerRegistrationStatus(x));

            entity.Property(x => x.RequestedUtc).IsRequired();
            entity.Property(x => x.ReviewedUtc);
            entity.Property(x => x.ReviewedByAdminUserId);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.ApprovedOwnerCode).HasMaxLength(30);

            entity.HasOne(x => x.User)
                .WithMany(x => x.OwnerRegistrationRequestsSubmitted)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ReviewedByAdminUser)
                .WithMany(x => x.OwnerRegistrationRequestsReviewed)
                .HasForeignKey(x => x.ReviewedByAdminUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.UserId, x.Status, x.RequestedUtc });
        });

        modelBuilder.Entity<LanguageOwnershipRequest>(entity =>
        {
            entity.ToTable("LanguageOwnershipRequests");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.OwnerUserId).IsRequired();
            entity.Property(x => x.LanguageId).IsRequired();

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbLanguageOwnershipRequestStatus(x), x => FromDbLanguageOwnershipRequestStatus(x));

            entity.Property(x => x.RequestedUtc).IsRequired();
            entity.Property(x => x.ReviewedUtc);
            entity.Property(x => x.ReviewedByAdminUserId);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);

            entity.HasOne(x => x.OwnerUser)
                .WithMany(x => x.LanguageOwnershipRequestsSubmitted)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Language)
                .WithMany(x => x.OwnershipRequests)
                .HasForeignKey(x => x.LanguageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ReviewedByAdminUser)
                .WithMany(x => x.LanguageOwnershipRequestsReviewed)
                .HasForeignKey(x => x.ReviewedByAdminUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.OwnerUserId, x.LanguageId, x.Status, x.RequestedUtc });
        });

        modelBuilder.Entity<UserFavorite>(entity =>
        {
            entity.ToTable("UserFavorites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.PoiId).IsRequired().HasMaxLength(20);
            entity.Property(x => x.CreatedUtc).IsRequired();

            // Mỗi user có thể đánh dấu yêu thích nhiều POI.
            entity.HasOne(x => x.User)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mỗi favorite phải tham chiếu đến một POI tồn tại.
            entity.HasOne(x => x.Poi)
                .WithMany()
                .HasForeignKey(x => x.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.PoiId }).IsUnique();
        });

        modelBuilder.Entity<UserTour>(entity =>
        {
            entity.ToTable("UserTours");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.PoiId).IsRequired().HasMaxLength(20);
            entity.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
            entity.Property(x => x.TriggerType).IsRequired().HasMaxLength(30);
            entity.Property(x => x.VisitedUtc).IsRequired();

            // Lưu lịch sử tour cho user để có thể khôi phục lại khi đăng nhập.
            entity.HasOne(x => x.User)
                .WithMany(x => x.Tours)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Poi)
                .WithMany()
                .HasForeignKey(x => x.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.VisitedUtc });
            entity.HasIndex(x => new { x.PoiId, x.VisitedUtc });
        });

        modelBuilder.Entity<TourList>(entity =>
        {
            entity.ToTable("TourList");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TourCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.TourName).HasMaxLength(150);
            entity.Property(x => x.OwnerUserId);
            entity.Property(x => x.IsPublic).IsRequired();
            entity.Property(x => x.PoiId).IsRequired().HasMaxLength(20);
            entity.Property(x => x.SortOrder).IsRequired();
            entity.Property(x => x.CreatedUtc).IsRequired();

            entity.HasOne(x => x.Poi)
                .WithMany(x => x.TourLists)
                .HasForeignKey(x => x.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.TourCode, x.SortOrder }).IsUnique();
            entity.HasIndex(x => new { x.TourCode, x.PoiId }).IsUnique();
            entity.HasIndex(x => x.IsPublic);
            entity.HasIndex(x => new { x.OwnerUserId, x.IsPublic });
        });

        modelBuilder.Entity<FoodItem>(entity =>
        {
            entity.ToTable("FoodItems");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PoiId).IsRequired().HasMaxLength(20);
            entity.Property(x => x.OwnerId);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(12, 2);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            entity.Property(x => x.IsAvailable).IsRequired();
            entity.Property(x => x.DisplayOrder).IsRequired();
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.Property(x => x.UpdatedUtc).IsRequired();

            entity.HasOne(x => x.Poi)
                .WithMany(x => x.FoodItems)
                .HasForeignKey(x => x.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Owner)
                .WithMany(x => x.ManagedFoodItems)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.PoiId, x.DisplayOrder });
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();

            entity.Property(x => x.Tier)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbSubscriptionTier(x), x => FromDbSubscriptionTier(x));

            entity.Property(x => x.BillingPeriod)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbBillingPeriod(x), x => FromDbBillingPeriod(x));

            entity.Property(x => x.PaymentStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbPaymentStatus(x), x => FromDbPaymentStatus(x));

            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            entity.Property(x => x.PaymentProvider).HasMaxLength(50);
            entity.Property(x => x.PaymentTransactionCode).HasMaxLength(120);
            entity.Property(x => x.PaidUtc);
            entity.Property(x => x.StartedUtc).IsRequired();
            entity.Property(x => x.ExpiresUtc).IsRequired();
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.Property(x => x.UpdatedUtc).IsRequired();

            entity.HasOne(x => x.User)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.ExpiresUtc });
            entity.HasIndex(x => new { x.UserId, x.PaymentStatus, x.ExpiresUtc });
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PlanCode).IsRequired().HasMaxLength(40);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);

            entity.Property(x => x.Tier)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbSubscriptionTier(x), x => FromDbSubscriptionTier(x));

            entity.Property(x => x.BillingPeriod)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbBillingPeriod(x), x => FromDbBillingPeriod(x));

            entity.Property(x => x.FixedPrice).HasPrecision(12, 2);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            entity.Property(x => x.MaxAccessiblePoiCount);
            entity.Property(x => x.MaxOwnerPoiCount);
            entity.Property(x => x.MaxActivationRadiusMeters).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.Property(x => x.UpdatedUtc).IsRequired();
            entity.Property(x => x.CreatedByAdminUserId).IsRequired();
            entity.Property(x => x.UpdatedByAdminUserId);

            entity.HasOne(x => x.CreatedByAdminUser)
                .WithMany(x => x.CreatedSubscriptionPlans)
                .HasForeignKey(x => x.CreatedByAdminUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedByAdminUser)
                .WithMany(x => x.UpdatedSubscriptionPlans)
                .HasForeignKey(x => x.UpdatedByAdminUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.PlanCode).IsUnique();
            entity.HasIndex(x => new { x.Tier, x.BillingPeriod }).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.SubscriptionId);

            entity.Property(x => x.PaymentProvider)
                .IsRequired()
                .HasMaxLength(30)
                .HasConversion(x => ToDbPaymentProviderType(x), x => FromDbPaymentProviderType(x));

            entity.Property(x => x.PaymentStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(x => ToDbPaymentStatus(x), x => FromDbPaymentStatus(x));

            entity.Property(x => x.TransactionCode).IsRequired().HasMaxLength(120);
            entity.Property(x => x.ProviderReferenceCode).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.Property(x => x.CompletedUtc);

            entity.HasOne(x => x.User)
                .WithMany(x => x.PaymentTransactions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Subscription)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.TransactionCode).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedUtc });
            entity.HasIndex(x => new { x.PaymentStatus, x.CreatedUtc });
        });

        modelBuilder.Entity<ActiveClientHeartbeat>(entity =>
        {
            entity.ToTable("ActiveClientHeartbeats");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(64)
                .ValueGeneratedNever();

            entity.Property(x => x.ClientType)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.UserId);

            entity.Property(x => x.LastSeenUtc)
                .IsRequired();

            entity.Property(x => x.SessionKey)
                .IsRequired()
                .HasMaxLength(120);

            entity.HasIndex(x => x.SessionKey).IsUnique();
            entity.HasIndex(x => new { x.ClientType, x.LastSeenUtc });
        });
    }

    private static string ToDbPoiType(PoiType value) => value switch
    {
        PoiType.Food => "food",
        PoiType.Visit => "visit",
        _ => "visit"
    };

    private static PoiType FromDbPoiType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "food" => PoiType.Food,
        "visit" => PoiType.Visit,
        "stayin" => PoiType.Visit,
        _ => PoiType.Visit
    };

    private static string ToDbPoiApprovalStatus(PoiApprovalStatus value) => value switch
    {
        PoiApprovalStatus.Pending => "pending",
        PoiApprovalStatus.Approved => "approved",
        PoiApprovalStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static PoiApprovalStatus FromDbPoiApprovalStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "approved" => PoiApprovalStatus.Approved,
        "rejected" => PoiApprovalStatus.Rejected,
        _ => PoiApprovalStatus.Pending
    };

    private static string ToDbUserRole(UserRole value) => value switch
    {
        UserRole.User => "user",
        UserRole.Owner => "owner",
        UserRole.Admin => "admin",
        _ => "user"
    };

    private static UserRole FromDbUserRole(string value) => value.Trim().ToLowerInvariant() switch
    {
        "owner" => UserRole.Owner,
        "admin" => UserRole.Admin,
        _ => UserRole.User
    };

    private static string ToDbOwnerRegistrationStatus(OwnerRegistrationStatus value) => value switch
    {
        OwnerRegistrationStatus.Pending => "pending",
        OwnerRegistrationStatus.Approved => "approved",
        OwnerRegistrationStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static OwnerRegistrationStatus FromDbOwnerRegistrationStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "approved" => OwnerRegistrationStatus.Approved,
        "rejected" => OwnerRegistrationStatus.Rejected,
        _ => OwnerRegistrationStatus.Pending
    };

    private static string ToDbLanguageOwnershipRequestStatus(LanguageOwnershipRequestStatus value) => value switch
    {
        LanguageOwnershipRequestStatus.Pending => "pending",
        LanguageOwnershipRequestStatus.Approved => "approved",
        LanguageOwnershipRequestStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static LanguageOwnershipRequestStatus FromDbLanguageOwnershipRequestStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "approved" => LanguageOwnershipRequestStatus.Approved,
        "rejected" => LanguageOwnershipRequestStatus.Rejected,
        _ => LanguageOwnershipRequestStatus.Pending
    };

    private static string ToDbSubscriptionTier(SubscriptionTier value) => value switch
    {
        SubscriptionTier.Free => "free",
        SubscriptionTier.Basic => "basic",
        SubscriptionTier.Premium => "premium",
        _ => "free"
    };

    private static SubscriptionTier FromDbSubscriptionTier(string value) => value.Trim().ToLowerInvariant() switch
    {
        "basic" => SubscriptionTier.Basic,
        "premium" => SubscriptionTier.Premium,
        _ => SubscriptionTier.Free
    };

    private static string ToDbBillingPeriod(BillingPeriod value) => value switch
    {
        BillingPeriod.Monthly => "monthly",
        BillingPeriod.Quarterly => "quarterly",
        BillingPeriod.Yearly => "yearly",
        _ => "monthly"
    };

    private static BillingPeriod FromDbBillingPeriod(string value) => value.Trim().ToLowerInvariant() switch
    {
        "quarterly" => BillingPeriod.Quarterly,
        "yearly" => BillingPeriod.Yearly,
        _ => BillingPeriod.Monthly
    };

    private static string ToDbPaymentStatus(PaymentStatus value) => value switch
    {
        PaymentStatus.Pending => "pending",
        PaymentStatus.Paid => "paid",
        PaymentStatus.Failed => "failed",
        PaymentStatus.Cancelled => "cancelled",
        PaymentStatus.Refunded => "refunded",
        _ => "pending"
    };

    private static PaymentStatus FromDbPaymentStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "paid" => PaymentStatus.Paid,
        "failed" => PaymentStatus.Failed,
        "cancelled" => PaymentStatus.Cancelled,
        "refunded" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending
    };

    private static string ToDbPaymentProviderType(PaymentProviderType value) => value switch
    {
        PaymentProviderType.VnPay => "vnpay",
        PaymentProviderType.Momo => "momo",
        PaymentProviderType.Stripe => "stripe",
        PaymentProviderType.Paypal => "paypal",
        PaymentProviderType.ZaloPay => "zalopay",
        PaymentProviderType.Other => "other",
        _ => "manual"
    };

    private static PaymentProviderType FromDbPaymentProviderType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "vnpay" => PaymentProviderType.VnPay,
        "momo" => PaymentProviderType.Momo,
        "stripe" => PaymentProviderType.Stripe,
        "paypal" => PaymentProviderType.Paypal,
        "zalopay" => PaymentProviderType.ZaloPay,
        "other" => PaymentProviderType.Other,
        _ => PaymentProviderType.Manual
    };
}
