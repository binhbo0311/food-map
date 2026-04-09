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
            entity.Property(x => x.Latitude).IsRequired();
            entity.Property(x => x.Longitude).IsRequired();
            entity.Property(x => x.ActivationRadius).IsRequired();
            entity.Property(x => x.Priority).IsRequired();
            entity.Property(x => x.QRCodeId).HasMaxLength(100);
        });

        modelBuilder.Entity<POITranslation>(entity =>
        {
            entity.ToTable("POITranslations");
            entity.HasKey(x => x.Id);

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
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
        });

        modelBuilder.Entity<UserFavorite>(entity =>
        {
            entity.ToTable("UserFavorites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.PoiId).IsRequired();
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
            entity.Property(x => x.PoiId).IsRequired();
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
    }
}
