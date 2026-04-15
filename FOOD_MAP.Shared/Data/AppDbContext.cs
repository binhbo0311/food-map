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

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    public DbSet<OwnerRegistrationRequest> OwnerRegistrationRequests => Set<OwnerRegistrationRequest>();

    public DbSet<LanguageOwnershipRequest> LanguageOwnershipRequests => Set<LanguageOwnershipRequest>();

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
}
