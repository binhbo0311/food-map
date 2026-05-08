using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Services;

public sealed class DataService : IDataService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private static readonly IReadOnlyDictionary<string, string> LegacyImageUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["images/nha-hat-thanh-pho.jpg"] = "https://picsum.photos/seed/nha_hat_thanh_pho/1200/800",
        ["images/bao-tang-thanh-pho.jpg"] = "https://picsum.photos/seed/bao_tang_thanh_pho/1200/800",
        ["images/cho-ben-thanh.jpg"] = "https://picsum.photos/seed/cho_ben_thanh/1200/800",
        ["images/city-opera-house.jpg"] = "https://picsum.photos/seed/city_opera_house/1200/800",
        ["images/city-museum.jpg"] = "https://picsum.photos/seed/city_museum/1200/800",
        ["images/ben-thanh-market.jpg"] = "https://picsum.photos/seed/ben_thanh_market/1200/800",
        ["images/pho-di-bo-nguyen-hue.jpg"] = "https://picsum.photos/seed/pho_di_bo_nguyen_hue/1200/800",
        ["images/buu-dien-trung-tam-sai-gon.jpg"] = "https://picsum.photos/seed/buu_dien_trung_tam_sai_gon/1200/800",
        ["images/dinh-doc-lap.jpg"] = "https://picsum.photos/seed/dinh_doc_lap/1200/800",
        ["images/nguyen-hue-walking-street.jpg"] = "https://picsum.photos/seed/nguyen_hue_walking_street/1200/800",
        ["images/saigon-central-post-office.jpg"] = "https://picsum.photos/seed/saigon_central_post_office/1200/800",
        ["images/independence-palace.jpg"] = "https://picsum.photos/seed/independence_palace/1200/800"
    };

    public DataService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Đảm bảo schema PostgreSQL được tạo từ model hiện tại.
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureTourListTableAsync(dbContext, cancellationToken);

        var hasBaseData = await dbContext.Pois.AnyAsync(cancellationToken)
            && await dbContext.Languages.AnyAsync(cancellationToken);

        // Nếu đã có dữ liệu POI cơ bản thì không seed lại để tránh ghi đè dữ liệu user.
        if (hasBaseData)
        {
            await NormalizeLegacyImageUrlsAsync(dbContext, cancellationToken);
            await EnsureDemoUserAsync(dbContext, cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var viLanguage = new Language
        {
            LanguageCode = "vi",
            LanguageName = "Tiếng Việt"
        };

        var enLanguage = new Language
        {
            LanguageCode = "en",
            LanguageName = "English"
        };

        dbContext.Languages.AddRange(viLanguage, enLanguage);

        var poiOperaHouse = new POI
        {
            Id = "VS-001",
            Type = PoiType.Visit,
            Latitude = 10.776889,
            Longitude = 106.700806,
            ActivationRadius = 120,
            Priority = 1,
            QRCodeId = "QR-ND-001",
            ApprovalStatus = PoiApprovalStatus.Approved,
            SubmittedUtc = DateTimeOffset.UtcNow
        };

        var poiMuseum = new POI
        {
            Id = "VS-002",
            Type = PoiType.Visit,
            Latitude = 10.780245,
            Longitude = 106.699020,
            ActivationRadius = 150,
            Priority = 2,
            QRCodeId = "QR-BT-002",
            ApprovalStatus = PoiApprovalStatus.Approved,
            SubmittedUtc = DateTimeOffset.UtcNow
        };

        var poiBenThanh = new POI
        {
            Id = "FD-001",
            Type = PoiType.Food,
            Latitude = 10.773500,
            Longitude = 106.704200,
            ActivationRadius = 100,
            Priority = 3,
            QRCodeId = "QR-CH-003",
            ApprovalStatus = PoiApprovalStatus.Approved,
            SubmittedUtc = DateTimeOffset.UtcNow
        };

        dbContext.Pois.AddRange(poiOperaHouse, poiMuseum, poiBenThanh);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.PoiTranslations.AddRange(
            new POITranslation
            {
                PoiId = poiOperaHouse.Id,
                LanguageId = viLanguage.Id,
                LocationName = "Nhà hát Thành phố",
                Description = "Công trình kiến trúc biểu tượng ở trung tâm Sài Gòn.",
                ImageUrl = "https://picsum.photos/seed/nha_hat_thanh_pho/1200/800",
                AudioFileUrl = "audio/vi/nha-hat-thanh-pho.mp3",
                TtsScript = "Bạn đang ở gần Nhà hát Thành phố. Đây là điểm đến nổi bật của khu trung tâm.",
                RichContentHtml = "<h3>Lịch sử nhanh</h3><p>Nhà hát Thành phố là công trình kiến trúc tiêu biểu tại trung tâm.</p>"
            },
            new POITranslation
            {
                PoiId = poiMuseum.Id,
                LanguageId = viLanguage.Id,
                LocationName = "Bảo tàng Thành phố",
                Description = "Bảo tàng lưu giữ nhiều hiện vật lịch sử và văn hóa.",
                ImageUrl = "https://picsum.photos/seed/bao_tang_thanh_pho/1200/800",
                AudioFileUrl = "audio/vi/bao-tang-thanh-pho.mp3",
                TtsScript = "Bạn đã đến gần Bảo tàng Thành phố. Hãy khám phá những câu chuyện lịch sử tại đây.",
                RichContentHtml = "<h3>Gợi ý tham quan</h3><ul><li>Khu hiện vật cổ</li><li>Khu lịch sử đô thị</li></ul>"
            },
            new POITranslation
            {
                PoiId = poiBenThanh.Id,
                LanguageId = viLanguage.Id,
                LocationName = "Chợ Bến Thành",
                Description = "Khu chợ nổi tiếng với ẩm thực và đặc sản địa phương.",
                ImageUrl = "https://picsum.photos/seed/cho_ben_thanh/1200/800",
                AudioFileUrl = "audio/vi/cho-ben-thanh.mp3",
                TtsScript = "Bạn đang ở gần Chợ Bến Thành. Đây là nơi lý tưởng để trải nghiệm ẩm thực Sài Gòn.",
                RichContentHtml = "<h3>Mẹo ăn uống</h3><p>Nên đi vào buổi chiều để trải nghiệm khu ẩm thực sôi động.</p>"
            },
            new POITranslation
            {
                PoiId = poiOperaHouse.Id,
                LanguageId = enLanguage.Id,
                LocationName = "City Opera House",
                Description = "A landmark architecture in the heart of Ho Chi Minh City.",
                ImageUrl = "https://picsum.photos/seed/city_opera_house/1200/800",
                AudioFileUrl = "audio/en/city-opera-house.mp3",
                TtsScript = "You are near the City Opera House, one of the most iconic spots in downtown Ho Chi Minh City.",
                RichContentHtml = "<h3>Quick history</h3><p>The City Opera House is one of the best-known colonial-era buildings downtown.</p>"
            },
            new POITranslation
            {
                PoiId = poiMuseum.Id,
                LanguageId = enLanguage.Id,
                LocationName = "City Museum",
                Description = "A museum preserving historical and cultural collections.",
                ImageUrl = "https://picsum.photos/seed/city_museum/1200/800",
                AudioFileUrl = "audio/en/city-museum.mp3",
                TtsScript = "You are close to the City Museum. Explore the historical stories preserved here.",
                RichContentHtml = "<h3>Visitor tips</h3><ul><li>Start from floor one</li><li>Allocate 45-60 minutes</li></ul>"
            },
            new POITranslation
            {
                PoiId = poiBenThanh.Id,
                LanguageId = enLanguage.Id,
                LocationName = "Ben Thanh Market",
                Description = "A famous market known for local food and souvenirs.",
                ImageUrl = "https://picsum.photos/seed/ben_thanh_market/1200/800",
                AudioFileUrl = "audio/en/ben-thanh-market.mp3",
                TtsScript = "You are near Ben Thanh Market, a great place to enjoy local food and shopping.",
                RichContentHtml = "<h3>What to try</h3><p>Look for local snacks and handmade souvenirs in the central lanes.</p>"
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsureDemoUserAsync(dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureTourListTableAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TourList" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "TourCode" character varying(50) NOT NULL,
                "TourName" character varying(150) NULL,
                "OwnerUserId" integer NULL,
                "IsPublic" boolean NOT NULL DEFAULT false,
                "PoiId" character varying(20) NOT NULL,
                "SortOrder" integer NOT NULL,
                "CreatedUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "FK_TourList_POIs_PoiId" FOREIGN KEY ("PoiId") REFERENCES "POIs" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TourList_Users_OwnerUserId" FOREIGN KEY ("OwnerUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "TourName" character varying(150);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "OwnerUserId" integer;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "IsPublic" boolean NOT NULL DEFAULT false;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'FK_TourList_Users_OwnerUserId'
                ) THEN
                    ALTER TABLE "TourList"
                    ADD CONSTRAINT "FK_TourList_Users_OwnerUserId"
                    FOREIGN KEY ("OwnerUserId") REFERENCES "Users"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TourList_TourCode_SortOrder"
            ON "TourList" ("TourCode", "SortOrder");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TourList_TourCode_PoiId"
            ON "TourList" ("TourCode", "PoiId");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_TourList_IsPublic"
            ON "TourList" ("IsPublic");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_TourList_OwnerUserId_IsPublic"
            ON "TourList" ("OwnerUserId", "IsPublic");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "TourList"
            SET "IsPublic" = true
            WHERE "IsPublic" = false
              AND "TourCode" LIKE 'PUB\\_%' ESCAPE '\\';
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "TourList" t
            SET "OwnerUserId" = parsed."OwnerId"
            FROM (
                SELECT
                    "Id",
                    split_part("TourCode", '_', 2)::integer AS "OwnerId"
                FROM "TourList"
                WHERE "OwnerUserId" IS NULL
                  AND "TourCode" LIKE 'USR\\_%' ESCAPE '\\'
                  AND split_part("TourCode", '_', 2) ~ '^[0-9]+$'
            ) parsed
            WHERE t."Id" = parsed."Id";
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "TourList" t
            SET "TourName" = parsed."NamePart"
            FROM (
                SELECT
                    "Id",
                    NULLIF(REGEXP_REPLACE(split_part("TourCode", '_', 3), '[^A-Z0-9_]', '', 'g'), '') AS "NamePart"
                FROM "TourList"
                WHERE "TourName" IS NULL
                  AND "TourCode" LIKE 'USR\\_%' ESCAPE '\\'
            ) parsed
            WHERE t."Id" = parsed."Id"
              AND parsed."NamePart" IS NOT NULL;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "TourList"
            SET "TourName" = REGEXP_REPLACE(split_part("TourCode", '_', 2), '[^A-Z0-9_]', '', 'g')
            WHERE "TourName" IS NULL
              AND "TourCode" LIKE 'PUB\\_%' ESCAPE '\\';
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "TourList" ("TourCode", "TourName", "OwnerUserId", "IsPublic", "PoiId", "SortOrder", "CreatedUtc")
            SELECT 'DEFAULT', 'DEFAULT', NULL, true, p."Id", ROW_NUMBER() OVER (ORDER BY p."Priority", p."Id"), NOW()
            FROM "POIs" p
            WHERE p."ApprovalStatus" = 'approved'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "TourList" t
                  WHERE t."TourCode" = 'DEFAULT'
              )
            ORDER BY p."Priority", p."Id"
            LIMIT 12;
            """,
            cancellationToken);
    }

    private static async Task NormalizeLegacyImageUrlsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var translations = await dbContext.PoiTranslations
            .Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
            .ToListAsync(cancellationToken);

        var hasChanges = false;

        foreach (var translation in translations)
        {
            if (Uri.TryCreate(translation.ImageUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                continue;
            }

            if (!LegacyImageUrlMap.TryGetValue(translation.ImageUrl, out var mappedUrl))
            {
                continue;
            }

            translation.ImageUrl = mappedUrl;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureDemoUserAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var hasAnyUser = await dbContext.Users.AnyAsync(cancellationToken);
        if (hasAnyUser)
        {
            return;
        }

        // Tạo tài khoản demo để test nhanh luồng đăng nhập User mode.
        dbContext.Users.Add(new User
        {
            UserName = "demo",
            DisplayName = "Demo User",
            PasswordHash = PasswordHasher.Hash("123456"),
            CreatedUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
